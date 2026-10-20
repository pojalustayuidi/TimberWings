// Assets/_Project/Scripts/UI/BuildWindow.cs (НЕ в Editor)
// Окно стройки в стиле UiKit (как окна пилорамы и прокачки). Всё строится из кода, настраивать в сцене ничего не нужно:
// создаётся при первом E на площадке (BuildSiteInteractable).
//
//   Слева (у каждого здания своё): значок здания, стадии 1-2-3 узлами, «Стадия N из M», название этапа и его описание,
//          плашки «время этапа» и «награда в опыте».
//   Справа: «Нужно для этапа» (бревна, доски, монеты: сдано, несёшь с собой, нужно), строка состояния с подсказкой
//          (или полоса стройки, пока этап идёт), кнопки «Сдать ресурсы» и «Играть».
//
// Всё берётся из BuildingController (и его BuildSiteData), окно ничего не хранит и не считает само.
// «Играть» вызывает BuildingController.StartStage(): сейчас это обычный таймер этапа, позже туда встанет мини-игра.
// Клавиши: Enter «Играть», Пробел «Сдать ресурсы», E или Esc закрыть. Мышь и тач: кнопки и крестик (клик мимо окна НЕ закрывает).
// Окно само закрывается, если игрок сошёл с площадки, начался разговор, открылась прокачка или стройку закрыли.
//
// Этап сдан (StageBuilt и Finished): окно закрывается СРАЗУ, дальше идёт показ стадии (BuildReveal), площадка заблокирована.
// Пока таймер этапа идёт, окно сворачивается в небольшую полоску внизу по центру экрана (название, полоса прогресса, «ещё N с»; на этом же месте потом появляется плашка показа стадии),
// чтобы было видно здание. E (или клик по полоске) раскрывает окно обратно, E, Esc и крестик в раскрытом окне снова сворачивают его.
// Свёрнутая полоска управление не забирает (MobileInput.ModalOpen читает IsOpen, а это только развёрнутое окно).
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public class BuildWindow : MonoBehaviour
{
    public static BuildWindow Instance { get; private set; }

    // развёрнутое окно (оно забирает управление: MobileInput.ModalOpen). Свёрнутая полоска сюда не входит
    public bool IsOpen { get { return open && !collapsed; } }
    // окно свёрнуто в полоску с прогрессом (этап идёт)
    public bool IsCollapsed { get { return open && collapsed; } }
    public static bool IsOpenFor(BuildingController b) { return Instance != null && Instance.open && Instance.bc == b; }

    // E на площадке: открыть окно; если оно уже открыто для этой стройки, то свёрнутое раскрывается, развёрнутое закрывается (или сворачивается, пока идёт этап)
    public static void Toggle(BuildingController b)
    {
        var win = GetOrCreate();
        if (win.open && win.bc == b)
        {
            if (win.collapsed) win.Expand(); else win.CloseOrCollapse();
        }
        else win.Open(b);
    }

    static BuildWindow GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("BuildWindow");
        return go.AddComponent<BuildWindow>();   // Awake выставит Instance
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    // ------------------------------------------------------------ цвета
    static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color OffText = new Color(0.86f, 0.88f, 0.92f, 1f);
    static readonly Color Sep = new Color(0.22f, 0.30f, 0.42f, 1f);
    static readonly Color NodeDim = new Color32(0x3A, 0x4A, 0x70, 255);
    static readonly Color NodeDone = new Color32(0x3C, 0x8A, 0x3B, 255);

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float WinW = 1120f, WinH = 800f, BS = 1.5f;      // окно и масштаб его рамки
    const float ContentW = 1042f, ContentH = 707f;         // внутренняя область: окно минус рамка
    const float LeftW = 400f, RightX = 420f, RightW = 622f;
    const float NeedH = 390f, StatusY = 404f, StatusH = 130f, BtnY = 560f, BtnH = 112f;
    const float NodesW = 340f;                              // ширина ряда стадий в левой карточке
    const float RowW = 574f, RowH = 76f, RowStep = 88f, RowTop = 76f;
    const float StripW = 340f, StripH = 72f, StripY = 70f;                    // компактная полоска внизу по центру (там же, где плашка BuildReveal)

    class Btn
    {
        public Button btn;
        public TMP_Text txt;
        public Color fg;
    }

    // строка ресурса: значок, полоса (сдано + несёшь с собой), счётчик
    class Row
    {
        public RectTransform root;
        public Image fill, preview, check;
        public TMP_Text count;
        public float shown, target, shownPreview, targetPreview;
    }

    // ------------------------------------------------------------ состояние
    BuildingController bc;
    PlayerInventory inv;
    bool open, built, subscribed;
    bool collapsed;                   // окно свёрнуто в полоску (этап идёт)
    float openK, stripK, refreshAt, punch = 1f, pulse;
    int openFrame, actionFrame = -1;
    int lastTotal = -1;
    string lastTitle = "";

    CanvasGroup overlay;
    RectTransform panel;
    Image headerIcon, bigIcon, barFill;
    TMP_Text titleText, stageCaption, stageName, stageDesc, timeText, xpText;
    TMP_Text needTitle, backpackText, statusText, barText;
    RectTransform nodesRoot, chipXp, barFrame, headerIconRt;
    Image[] nodeRing = new Image[0], nodeFill = new Image[0], nodeCheck = new Image[0], links = new Image[0];
    TMP_Text[] nodeNum = new TMP_Text[0];
    RectTransform targetNode;
    Row rowWood, rowPlank, rowCoin;
    Btn deposit, play;
    RectTransform playRt;
    bool canPlayNow;

    // свёрнутая полоска: название, полоса прогресса, «ещё N с»
    RectTransform stripRt;
    CanvasGroup stripGroup;
    Image stripFill;
    TMP_Text stripTitle, stripTime;
    string stripTitleShown = "";

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { Instance = this; }

    void OnDestroy()
    {
        Detach();
        if (Instance == this) Instance = null;
    }

    public void Open(BuildingController b)
    {
        if (b == null || !b.UseWindow || b.IsRevealing) return;   // на время показа стадии площадка закрыта
        if (UpgradeWindow.Instance != null && UpgradeWindow.Instance.IsOpen) return;   // два окна сразу не нужны
        if (MobileInput.ModalOpen) return;

        EnsureBuilt();
        Detach();
        bc = b;
        Attach();
        if (inv == null) inv = PlayerInventory.Instance;

        open = true;
        collapsed = false;
        openFrame = Time.frameCount;      // E, которой открыли окно, его же не закроет
        lastTotal = -1;
        punch = 0f;
        Refresh();
    }

    public void Close()
    {
        open = false;
        collapsed = false;
        Detach();
    }

    // полоска -> окно (E или клик по полоске)
    void Expand()
    {
        if (!open || !collapsed || bc == null) return;
        if (UpgradeWindow.Instance != null && UpgradeWindow.Instance.IsOpen) return;
        if (MobileInput.ModalOpen) return;
        collapsed = false;
        openFrame = Time.frameCount;      // E, которой раскрыли окно, его же не свернёт
        punch = 0f;
        Refresh();
    }

    // окно -> полоска (этап идёт, нужно видеть здание)
    void Collapse()
    {
        if (!open || collapsed) return;
        collapsed = true;
        refreshAt = 0f;
    }

    // E, Esc, крестик и клик мимо окна: пока этап идёт, окно сворачивается в полоску, иначе закрывается совсем
    void CloseOrCollapse()
    {
        if (bc != null && bc.StageRunning && !bc.IsFinished && !bc.IsRevealing) Collapse(); else Close();
    }

    void Attach()
    {
        if (bc == null || subscribed) return;
        bc.StageBuilt += OnStageBuilt;
        subscribed = true;
    }

    void Detach()
    {
        if (bc != null && subscribed) bc.StageBuilt -= OnStageBuilt;
        subscribed = false;
    }

    // этап сдан (и обычный, и последний): окно закрывается сразу. Дальше идёт показ стадии (BuildReveal), «Стадия N готова» пишет его плашка.
    // Раньше окно оставалось открытым, и игрок жал «Сдать» и «Играть» подряд, пролетая все визуальные стадии
    void OnStageBuilt() { Close(); }

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
            if (ShouldClose()) Close();
            else if (!collapsed)
            {
                // выбранная кнопка реагировала бы ещё и на Enter: снимаем выделение, Enter обрабатывает только Hotkeys
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                Hotkeys();
            }
            // свёрнутая полоска клавиш не слушает: E раскрывает её через InteractionHub (BuildSiteInteractable)
            else if (!bc.StageRunning) Expand();   // этап вдруг не идёт (не должно случаться): полоске нечего показывать, возвращаем окно
        }

        float dt = Time.unscaledDeltaTime;
        openK = Mathf.MoveTowards(openK, (open && !collapsed) ? 1f : 0f, dt / 0.15f);
        stripK = Mathf.MoveTowards(stripK, (open && collapsed) ? 1f : 0f, dt / 0.2f);

        bool show = openK > 0.001f;
        bool showStrip = stripK > 0.001f;
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (stripRt.gameObject.activeSelf != showStrip) stripRt.gameObject.SetActive(showStrip);
        if (!show && !showStrip) return;

        if (show)
        {
            overlay.alpha = openK;
            Animate();
        }
        if (showStrip) AnimateStrip();

        if (bc != null)
        {
            if (show && Time.unscaledTime >= refreshAt) Refresh();
            if (show) UpdateBar();
            if (showStrip) UpdateStrip();
        }
    }

    bool ShouldClose()
    {
        if (bc == null || !bc.isActiveAndEnabled) return true;
        if (bc.IsFinished || bc.IsRevealing) return true;   // этап сдан: окно уже закрылось по StageBuilt, это страховка
        if (bc.IsLocked || !bc.PlayerInside) return true;
        if (DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return true;
        var u = UpgradeWindow.Instance;
        return u != null && u.IsOpen;
    }

    void Hotkeys()
    {
        if (Time.frameCount == openFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(MobileInput.ActionKey)) { CloseOrCollapse(); return; }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) DoPlay();
        if (Input.GetKeyDown(KeyCode.Space)) DoDeposit();
    }

    // ------------------------------------------------------------ действия
    void DoDeposit()
    {
        // два нажатия в один кадр (клик и Пробел) обрабатываются один раз
        if (bc == null || Time.frameCount == actionFrame) return;
        actionFrame = Time.frameCount;
        if (inv == null) inv = PlayerInventory.Instance;
        if (bc.DepositAll(inv) > 0)
        {
            SfxManager.Play("coin", 0.7f, 1f);
            punch = 0f;
        }
        Refresh();
    }

    void DoPlay()
    {
        if (bc == null || Time.frameCount == actionFrame) return;
        actionFrame = Time.frameCount;
        // StartStage сам не даст запустить этап второй раз
        if (bc.StartStage())
        {
            SfxManager.Play("hint", 0.7f, 1.1f);
            punch = 0f;
            Collapse();   // этап пошёл: окно уходит в полоску, здание на виду
        }
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

    static string Wood(int n) { return UiDraw.Plural(n, "бревно", "бревна", "бревен"); }
    static string Plank(int n) { return UiDraw.Plural(n, "доску", "доски", "досок"); }
    static string Coin(int n) { return UiDraw.Plural(n, "монету", "монеты", "монет"); }

    static string FormatTime(float sec)
    {
        if (sec >= 10f) return Mathf.RoundToInt(sec) + " с";
        return sec.ToString("0.0") + " с";
    }

    static Sprite IconOf(BuildingController b)
    {
        if (b.data != null && b.data.icon != null) return b.data.icon;
        var v = b.GetComponent<BuildSiteTagView>();
        if (v != null && v.buildingIcon != null) return v.buildingIcon;
        return UiKit.Icon(UiIcon.Log);
    }

    void Refresh()
    {
        refreshAt = Time.unscaledTime + 0.1f;
        if (!built || bc == null) return;
        if (inv == null) inv = PlayerInventory.Instance;

        bool done = bc.IsFinished;
        bool running = bc.StageRunning;
        int cur = bc.StageCurrent;
        int tot = bc.StageTotal;
        bool staged = tot > 1;

        // ----- шапка: у каждого здания своё название и значок
        string title = bc.BuildingTitle.ToUpperInvariant();
        if (title != lastTitle)
        {
            lastTitle = title;
            titleText.text = title;
            var icon = IconOf(bc);
            headerIcon.sprite = icon;
            bigIcon.sprite = icon;
            // значок встаёт слева от названия, какой бы длины оно ни было
            float w = Mathf.Min(titleText.GetPreferredValues(title).x, 330f);
            headerIconRt.anchoredPosition = new Vector2(WinW * 0.5f + 24f - w * 0.5f - 34f, -27f);
        }

        // ----- левая карточка: стадии, название и описание этапа
        if (tot != lastTotal) { RebuildNodes(staged ? tot : 0); lastTotal = tot; }
        nodesRoot.gameObject.SetActive(staged);
        stageCaption.gameObject.SetActive(staged);
        targetNode = null;
        for (int i = 0; i < nodeRing.Length; i++)
        {
            bool reached = i < cur;
            bool target = i == cur && !done;
            nodeRing[i].color = reached ? UiKit.Good : (target ? UiKit.Brass : NodeDim);
            nodeFill[i].color = reached ? NodeDone : (target ? UiKit.Navy : UiKit.Ink);
            nodeCheck[i].enabled = reached;
            nodeNum[i].enabled = !reached;
            nodeNum[i].color = target ? UiKit.BrassHi : Dim;
            nodeRing[i].rectTransform.localScale = Vector3.one;
            if (target) targetNode = nodeRing[i].rectTransform;
            if (i > 0) links[i - 1].color = reached ? UiKit.Good : NodeDim;
        }
        if (staged)
            Set(stageCaption, done ? "<color=#6BCB5A>Построено полностью</color>" : "Стадия " + cur + " из " + tot);

        string name = bc.StepName;
        if (string.IsNullOrEmpty(name) || name == "Этап") name = staged ? "Стадия " + (cur + 1) : "Строительство";
        Set(stageName, done ? "Готово!" : name);

        string desc = bc.StepDescription;
        if (string.IsNullOrEmpty(desc)) desc = "Сдай материалы и нажми «Играть», чтобы построить.";
        Set(stageDesc, done ? "Здание построено и готово к использованию." : desc);

        Set(timeText, FormatTime(bc.BuildTime));
        int xp = bc.StepXp;
        chipXp.gameObject.SetActive(xp > 0);
        Set(xpText, "<color=#FFD66B>+" + xp + " XP</color>");

        // ----- правая карточка: что нужно сдать
        int wood = inv != null ? inv.woodCount : 0;
        int planks = inv != null ? inv.planksCount : 0;
        int coins = inv != null ? inv.coinsCount : 0;
        int shownRows = 0;
        shownRows += FillRow(rowWood, shownRows, bc.Deposited, wood, bc.CostTotal, done);
        shownRows += FillRow(rowPlank, shownRows, bc.DepositedPlanks, planks, bc.PlankTotal, done);
        int haveCoins = bc.DepositedCoins >= bc.CoinTotal ? bc.CoinTotal : bc.DepositedCoins;
        shownRows += FillRow(rowCoin, shownRows, haveCoins, coins, bc.CoinTotal, done);
        needTitle.gameObject.SetActive(!done);
        Set(backpackText, done ? "" : BackpackLine(wood, planks, coins));

        // ----- состояние и кнопки
        bool canDeposit = bc.CanDeposit(inv);
        canPlayNow = bc.CanPlay;

        string status;
        if (done) status = "<color=#6BCB5A>Построено!</color>";
        else if (running) status = "<color=#FFD66B>Строим…</color>";
        else if (bc.AllDeposited) status = "<color=#6BCB5A>Всё сдано. Жми «Играть»!</color>";
        else if (canDeposit) status = "<color=#6BCB5A>Нажми «Сдать ресурсы»</color>";
        else status = "<color=#FFD66B>" + MissingText() + "</color>";
        Set(statusText, status);

        barFrame.gameObject.SetActive(running && !done);

        SetBtn(deposit, "Сдать ресурсы", canDeposit);
        SetBtn(play, done ? "Готово" : (running ? "Строим…" : "Играть"), canPlayNow);
    }

    // чего не хватает: сначала брёвна, потом доски, потом монеты
    string MissingText()
    {
        int w = bc.CostTotal - bc.Deposited;
        if (w > 0) return "Принеси ещё " + w + " " + Wood(w);
        int p = bc.PlankTotal - bc.DepositedPlanks;
        if (p > 0) return "Принеси ещё " + p + " " + Plank(p);
        int c = bc.CoinTotal - bc.DepositedCoins;
        if (c > 0) return "Нужно ещё " + c + " " + Coin(c);
        return "";
    }

    string BackpackLine(int wood, int planks, int coins)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (bc.CostTotal > 0) parts.Add("бревна <color=#6BCB5A>" + wood + "</color>");
        if (bc.PlankTotal > 0) parts.Add("доски <color=#6BCB5A>" + planks + "</color>");
        if (bc.CoinTotal > 0) parts.Add("монеты <color=#6BCB5A>" + coins + "</color>");
        return parts.Count == 0 ? "" : "В рюкзаке: " + string.Join("  ·  ", parts.ToArray());
    }

    // заполнить строку ресурса. have = сдано, carry = несёшь с собой, total = нужно. Возвращает 1, если строка показана
    int FillRow(Row r, int index, int have, int carry, int total, bool done)
    {
        bool on = total > 0 && !done;
        if (r.root.gameObject.activeSelf != on) r.root.gameObject.SetActive(on);
        if (!on) return 0;

        r.root.anchoredPosition = new Vector2(24f, -(RowTop + index * RowStep));

        int add = Mathf.Clamp(carry, 0, Mathf.Max(0, total - have));
        r.target = Mathf.Clamp01(have / (float)total);
        r.targetPreview = Mathf.Clamp01((have + add) / (float)total);

        bool full = have >= total;
        r.check.enabled = full;
        string col = full ? "#6BCB5A" : "#FFFFFF";
        string plus = (!full && add > 0) ? " <size=75%><color=#F5C542>+" + add + "</color></size>" : "";
        Set(r.count, "<color=" + col + ">" + have + "</color>" + plus + "<color=#AEB9D6>/" + total + "</color>");
        return 1;
    }

    // полоса стройки идёт каждый кадр: она плавная
    void UpdateBar()
    {
        if (bc == null || !bc.StageRunning || bc.IsFinished) return;
        float p = bc.Progress;
        barFill.fillAmount = p;
        barFill.gameObject.SetActive(p > 0.02f);
        float left = Mathf.Max(0f, (1f - p) * bc.BuildTime);
        Set(barText, bc.IsBuilding ? "ещё " + FormatTime(left) : "Встань на площадку");
    }

    // полоска идёт каждый кадр, как и полоса в окне
    void UpdateStrip()
    {
        if (bc == null) return;
        float p = bc.Progress;
        stripFill.fillAmount = p;
        stripFill.gameObject.SetActive(p > 0.02f);
        float left = Mathf.Max(0f, (1f - p) * bc.BuildTime);
        Set(stripTime, bc.IsBuilding ? "ещё " + FormatTime(left) : "Встань на площадку");

        // «Хижина · стадия 2/3»: номер той стадии, которая строится сейчас
        int tot = bc.StageTotal;
        string t = bc.BuildingTitle;
        if (tot > 1) t += " · " + Mathf.Min(bc.StageCurrent + 1, tot) + "/" + tot;
        if (t != stripTitleShown) { stripTitleShown = t; stripTitle.text = t; }
    }

    // полоска мягко выезжает снизу
    void AnimateStrip()
    {
        float e = UiDraw.EaseOutCubic(stripK);
        stripGroup.alpha = stripK;
        stripGroup.blocksRaycasts = stripK > 0.5f;
        stripRt.anchoredPosition = new Vector2(0f, StripY + Mathf.Lerp(-30f, 0f, e));
    }

    // лёгкая анимация: открытие, «подпрыгивание» при событиях, дыхание кнопки «Играть» и следующей стадии
    void Animate()
    {
        float dt = Time.unscaledDeltaTime;
        pulse += dt * 2.4f;
        punch = Mathf.Min(1f, punch + dt / 0.3f);

        float s = Mathf.Lerp(0.94f, 1f, UiDraw.EaseOutCubic(openK));
        s *= 1f + 0.03f * Mathf.Sin(punch * Mathf.PI) * (1f - punch);
        panel.localScale = Vector3.one * s;

        if (playRt != null)
            playRt.localScale = Vector3.one * (canPlayNow ? 1f + 0.03f * (0.5f + 0.5f * Mathf.Sin(pulse * 1.6f)) : 1f);
        if (targetNode != null)
            targetNode.localScale = Vector3.one * (1f + 0.14f * (0.5f + 0.5f * Mathf.Sin(pulse * 1.6f)));

        float k = 1f - Mathf.Exp(-10f * dt);
        AnimRow(rowWood, k); AnimRow(rowPlank, k); AnimRow(rowCoin, k);
    }

    static void AnimRow(Row r, float k)
    {
        if (r == null || !r.root.gameObject.activeSelf) return;
        r.shown = Mathf.Lerp(r.shown, r.target, k);
        r.shownPreview = Mathf.Lerp(r.shownPreview, r.targetPreview, k);
        r.fill.fillAmount = r.shown;
        r.preview.fillAmount = r.shownPreview;
    }

    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        var cgo = new GameObject("BuildCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29;     // ниже окна прокачки (30), как остальные окна
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

        BuildInfoCard(content);
        BuildNeedCard(content);
        BuildStatusCard(content);
        BuildButtons(content);

        var hint = UiKit.MakeLabel(ov.transform, "Enter играть   ·   Пробел сдать ресурсы   ·   E или Esc закрыть", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(1100, 34));

        ov.gameObject.SetActive(false);

        BuildStrip(cgo.transform);
        built = true;
    }

    void BuildHeader()
    {
        headerIcon = UiKit.MakeIcon(panel, UiIcon.Log, 44f, "TitleIcon");
        headerIcon.preserveAspect = true;
        headerIconRt = headerIcon.rectTransform;
        TLC(headerIconRt, WinW * 0.5f - 112f, 27, 44, 44);

        titleText = UiKit.MakeLabel(panel, "", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(titleText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(24, -8), new Vector2(360, 40));
        AutoSize(titleText, 20f, 34f);

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.navigation = new Navigation { mode = Navigation.Mode.None };
        close.onClick.AddListener(CloseOrCollapse);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
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

        var panel = UiKit.MakePanel(stripRt, new Vector2(StripW, StripH), false, 0.7f, "StripPanel");
        Stretch(panel.rectTransform);
        panel.raycastTarget = true;
        // клик (тап) по полоске раскрывает окно
        var click = panel.gameObject.AddComponent<Button>();
        click.transition = Selectable.Transition.None;
        click.targetGraphic = panel;
        click.navigation = new Navigation { mode = Navigation.Mode.None };
        click.onClick.AddListener(Expand);

        stripTitle = Lbl(stripRt, "", 20f, Color.white, TextAlignmentOptions.Left, true, 14, 7, StripW - 70f, 26);

        Image fill;
        var bar = UiKit.MakeBar(stripRt, UiBar.Amber, new Vector2(StripW - 28f, 26f), out fill, 1f);
        TL(bar.rectTransform, 14, 38, StripW - 28f, 26f);
        stripFill = fill;
        stripFill.fillAmount = 0f;
        stripTime = UiKit.MakeLabel(bar.transform, "", 16f, Color.white, TextAlignmentOptions.Center, true);

        // «E» справа вверху: подсказка, чем раскрыть
        var key = UiKit.MakeKeycap(stripRt, MobileInput.ActionKey.ToString(), 30f);
        Anch(key.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -6f), new Vector2(30f, 30f));

        stripRt.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ левая карточка: здание и этап
    void BuildInfoCard(Transform parent)
    {
        var card = UiKit.MakePanel(parent, new Vector2(LeftW, ContentH), false, 0.6f, "Info");
        TL(card.rectTransform, 0, 0, LeftW, ContentH);
        var c = card.transform;

        // крупный значок здания в тёмной «лунке»
        var slot = UiKit.MakeSlot(c, 180f, 1.1f, "BuildingSlot");
        TLC(slot.rectTransform, LeftW * 0.5f, 118, 180, 180);
        bigIcon = UiKit.MakeIcon(slot.transform, UiIcon.Log, 132f, "BuildingIcon");
        bigIcon.rectTransform.anchoredPosition = Vector2.zero;

        // стадии 1 - 2 - 3 нумерованными узлами
        nodesRoot = NewRT("Nodes", c);
        TLC(nodesRoot, LeftW * 0.5f, 252, NodesW, 48);
        stageCaption = Lbl(c, "", 26f, UiKit.BrassHi, TextAlignmentOptions.Center, true, 20, 286, LeftW - 40, 34);
        stageCaption.richText = true;

        Divider(c, 30, 336, LeftW - 60);

        // название этапа и описание (описание переносится по словам)
        stageName = Lbl(c, "", 34f, Color.white, TextAlignmentOptions.Center, true, 20, 350, LeftW - 40, 48);
        stageDesc = UiKit.MakeLabel(c, "", 23f, Soft, TextAlignmentOptions.Top, false);
        TL(stageDesc.rectTransform, 30, 406, LeftW - 60, 130);
        stageDesc.enableAutoSizing = true;
        stageDesc.fontSizeMin = 16f;
        stageDesc.fontSizeMax = 23f;
        stageDesc.enableWordWrapping = true;

        Divider(c, 30, 548, LeftW - 60);

        // плашки: время этапа и награда
        MakeChip(c, UiIcon.Clock, 24, 570, 168, out timeText);
        chipXp = MakeChip(c, UiIcon.Star, 208, 570, 168, out xpText);
    }

    RectTransform MakeChip(Transform parent, UiIcon icon, float x, float y, float w, out TMP_Text label)
    {
        var chip = UiKit.MakeImage(parent, "Chip_" + icon, UiKit.Slot, true, 0.6f);
        TL(chip.rectTransform, x, y, w, 64);
        var ic = UiKit.MakeIcon(chip.transform, icon, 38f, "Icon");
        Anch(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(38, 38));
        label = UiKit.MakeLabel(chip.transform, "", 28f, Color.white, TextAlignmentOptions.Left, true);
        label.richText = true;
        label.rectTransform.offsetMin = new Vector2(58f, 0f);
        label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        AutoSize(label, 16f, 28f);
        return chip.rectTransform;
    }

    // нумерованные узлы стадий со звеньями между ними
    void RebuildNodes(int total)
    {
        for (int i = nodesRoot.childCount - 1; i >= 0; i--) Destroy(nodesRoot.GetChild(i).gameObject);
        nodeRing = new Image[total]; nodeFill = new Image[total]; nodeCheck = new Image[total];
        nodeNum = new TMP_Text[total];
        links = new Image[Mathf.Max(0, total - 1)];
        targetNode = null;
        if (total <= 0) return;

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
            var num = UiKit.MakeLabel(ring.transform, (i + 1).ToString(), 24f, Color.white, TextAlignmentOptions.Center, true);
            var ck = UiKit.MakeIcon(ring.transform, UiIcon.Check, 26f, "Check");
            ck.rectTransform.anchoredPosition = Vector2.zero;
            nodeRing[i] = ring; nodeFill[i] = fill; nodeNum[i] = num; nodeCheck[i] = ck;
        }
    }

    // ------------------------------------------------------------ правая колонка
    void BuildNeedCard(Transform parent)
    {
        var card = UiKit.MakePanel(parent, new Vector2(RightW, NeedH), false, 0.6f, "Need");
        TL(card.rectTransform, RightX, 0, RightW, NeedH);
        var t = card.transform;

        needTitle = Lbl(t, "Нужно для этапа", 30f, Color.white, TextAlignmentOptions.Left, true, 24, 18, RowW, 42);

        rowWood = MakeRow(t, UiIcon.Log, UiBar.Green);
        rowPlank = MakeRow(t, UiIcon.Plank, UiBar.Blue);
        rowCoin = MakeRow(t, UiIcon.Coin, UiBar.Amber);

        backpackText = Lbl(t, "", 22f, Soft, TextAlignmentOptions.Left, false, 24, NeedH - 50, RowW, 32);
        backpackText.richText = true;
    }

    Row MakeRow(Transform parent, UiIcon icon, UiBar color)
    {
        var r = new Row();
        r.root = NewRT("Row_" + icon, parent);
        Anch(r.root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -RowTop), new Vector2(RowW, RowH));

        // значок ресурса в тёмной «лунке», поверх него галочка, когда всё сдано
        var slot = UiKit.MakeSlot(r.root, 72f, 0.8f, "Slot");
        TL(slot.rectTransform, 0, 2, 72, 72);
        var ic = UiKit.MakeIcon(slot.transform, icon, 52f, "Icon");
        ic.rectTransform.anchoredPosition = Vector2.zero;
        r.check = UiKit.MakeIcon(slot.transform, UiIcon.Check, 32f, "Done");
        Anch(r.check.rectTransform, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-6, 8), new Vector2(32, 32));
        r.check.enabled = false;

        // полоса: заливка «сдано» и бледная заливка «сдано + то, что несёшь с собой»
        Image fillImg;
        var bar = UiKit.MakeBar(r.root, color, new Vector2(330, 34), out fillImg, 1f);
        TL(bar.rectTransform, 88, 21, 330, 34);
        fillImg.fillAmount = 0f;
        r.fill = fillImg;

        var pv = UiKit.MakeImage(bar.transform, "Preview", UiKit.BarFill(color), false);
        pv.type = Image.Type.Filled; pv.fillMethod = Image.FillMethod.Horizontal; pv.fillOrigin = 0; pv.fillAmount = 0f;
        pv.color = new Color(1f, 1f, 1f, 0.4f);
        var prt = pv.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(5f, 5f); prt.offsetMax = new Vector2(-5f, -5f);
        pv.transform.SetSiblingIndex(0);                       // под заливкой «сдано»
        r.preview = pv;

        r.count = Lbl(r.root, "", 34f, Color.white, TextAlignmentOptions.Right, true, 430, 14, 144, 48);
        r.count.richText = true;
        return r;
    }

    void BuildStatusCard(Transform parent)
    {
        var card = UiKit.MakePanel(parent, new Vector2(RightW, StatusH), false, 0.6f, "Status");
        TL(card.rectTransform, RightX, StatusY, RightW, StatusH);
        var t = card.transform;

        statusText = Lbl(t, "", 30f, Color.white, TextAlignmentOptions.Center, true, 24, 16, RowW, 44);
        statusText.richText = true;

        // полоса стройки: видна, только пока этап идёт
        Image fill;
        barFrame = UiKit.MakeBar(t, UiBar.Amber, new Vector2(RowW, 38), out fill, 1f).rectTransform;
        TL(barFrame, 24, 68, RowW, 38);
        barFill = fill;
        barFill.fillAmount = 0f;
        barText = UiKit.MakeLabel(barFrame, "", 24f, Color.white, TextAlignmentOptions.Center, true);
        barFrame.gameObject.SetActive(false);
    }

    void BuildButtons(Transform parent)
    {
        deposit = MakeBtn(parent, "Сдать ресурсы", UiBtn.Amber, new Vector2(270, BtnH), 30f, DoDeposit);
        TL(deposit.btn.GetComponent<RectTransform>(), RightX, BtnY, 270, BtnH);
        AddBtnIcon(deposit, UiIcon.Box, 44f);
        AutoSize(deposit.txt, 18f, 30f);

        play = MakeBtn(parent, "Играть", UiBtn.Green, new Vector2(336, BtnH), 46f, DoPlay);
        playRt = play.btn.GetComponent<RectTransform>();
        TL(playRt, RightX + 286f, BtnY, 336, BtnH);
        AddBtnIcon(play, UiIcon.Play, 52f);
        AutoSize(play.txt, 22f, 46f);
    }

    // ------------------------------------------------------------ мелкие помощники (как в PlankWorkshopWindow)
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
