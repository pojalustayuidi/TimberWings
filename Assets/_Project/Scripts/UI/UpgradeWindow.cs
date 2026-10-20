// Assets/_Project/Scripts/UI/UpgradeWindow.cs (НЕ в Editor)
// Окно прокачки (GDD раздел 3) в стиле UiKit. Всё строится из кода, ничего настраивать в сцене не нужно (StoryFlow создаёт его сам).
//   Кнопка «Прокачка» внизу справа. Пока сюжет её не открыл, серая с подписью «закрыто».
//   Слева: 6 прокачек (сила удара, скорость бега, рюкзак, двойная добыча, торг, пила): уровень точками, «до → после», цена, кнопка.
//   Справа: дерево навыков за очки уровня (SkillTreeUi, логика в PlayerSkills): 3 ветки × 3 яруса, клик по узлу показывает карточку внизу.
//   Старые таланты за монеты удалены.
//   Клавиши: U открыть/закрыть, 1-6 купить за монеты, Shift+1-6 потратить бесплатное очко, Esc закрыть.
// Логика покупок живёт в PlayerUpgrades (TryBuy / TryUseFreePoint) и PlayerSkills, окно только показывает и вызывает её.
// Для квестов: Unlock(), SetHighlight(тип), SetOnly(тип) (пока задание идёт, можно купить только его), HasOpened.
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

[DefaultExecutionOrder(-5)]
public class UpgradeWindow : MonoBehaviour
{
    public static UpgradeWindow Instance { get; private set; }

    public KeyCode toggleKey = KeyCode.U;

    public bool IsUnlocked { get { return unlocked; } }
    public bool IsOpen { get { return open; } }
    public bool HasOpened { get; private set; }

    // ------------------------------------------------------------ API для квестов
    public void Unlock() { unlocked = true; }
    public void Lock() { unlocked = false; if (open) SetOpen(false); }
    public void SetHighlight(int type) { highlight = type; }      // -1 = нет подсветки
    public void SetOnly(int type) { only = type; }                // -1 = можно покупать всё

    // Открыть окно и подсветить строку (из окна пилорамы: «Прокачать пилу»). Подсветка снимается при закрытии.
    public void OpenFor(int type)
    {
        if (!unlocked) { toastUntil = Time.unscaledTime + 2f; return; }
        if (highlight < 0 && type >= 0) { highlight = type; tempHighlight = true; }
        SetOpen(true);
    }

    // ------------------------------------------------------------ данные
    static readonly string[] Names =
    {
        "Сила удара", "Скорость бега", "Вместимость рюкзака", "Двойная добыча", "Выгодный торг", "Скорость пилы"
    };

    static readonly UiIcon[] RowIcons = { UiIcon.Axe, UiIcon.Boot, UiIcon.Backpack, UiIcon.Log, UiIcon.Coin, UiIcon.Saw };

    // число из PlayerUpgrades.Calc -> текст
    static string Fmt(int type, float v)
    {
        switch (type)
        {
            case 0: return v.ToString("0.0") + " с";
            case 1: return (GameBalance.PlayerSpeed * v).ToString("0.0") + " м/с";
            case 2: { int n = Mathf.RoundToInt(v); return n + " " + UiDraw.Plural(n, "бревно", "бревна", "бревен"); }
            case 3: return Mathf.RoundToInt(v * 100f) + "%";
            case 4: return "+" + Mathf.RoundToInt((v - 1f) * 100f) + "%";
            default: return v.ToString("0.0") + " с";
        }
    }

    // ------------------------------------------------------------ цвета
    static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    static readonly Color Bad = new Color32(0xE0, 0x6A, 0x5A, 255);
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float WinW = 1400f, WinH = 900f, BS = 1.5f;        // окно и масштаб его рамки
    const float LeftW = 620f, RightW = 682f, PanelH = 807f;  // внутренняя область 1322 x 807
    const float RowW = 570f, RowH = 118f, RowGap = 10f, RowTop = 22f;

    class Row
    {
        public Image glow, bg;
        public TMP_Text name, desc, price, buyTxt;
        public GameObject priceGo, ptGo;
        public Image[] pips;
        public Button buy, pt;
    }

    bool unlocked, open, built, tempHighlight;
    int highlight = -1, only = -1;
    float openK, refreshAt, toastUntil, flashUntil;
    int flashRow = -1;
    // кнопка в HUD справа внизу (кнопка питомцев ставится левее неё: PetCollectionWindow читает эти же числа)
    public const float HudBtnW = 280f, HudBtnH = 88f, HudRight = 40f, HudBottom = 64f, HudGap = 14f;
    Image hudIcon, hudPts;
    RectTransform hudPtsRt;
    TMP_Text hudPtsText;
    UiIcon hudIconShown = UiIcon.Lock;
    UiBtn hudStyle = UiBtn.Gray;
    bool hudStyleSet;

    CanvasGroup overlay;
    RectTransform panel;
    Image hudImg;
    TMP_Text hudText, coinsText, pointsText;
    readonly Row[] rows = new Row[6];
    SkillTreeUi skillUi;   // правая панель: дерево навыков

    PlayerUpgrades up;
    PlayerInventory inv;

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        EnsureEventSystem();
        Build();
        Refresh();
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    bool FindRefs()
    {
        if (up == null) up = PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance : FindFirstObjectByType<PlayerUpgrades>();
        if (inv == null) inv = PlayerInventory.Instance;
        return up != null && inv != null;
    }

    void Update()
    {
        if (!built) return;

        if (!DialogueSequence.AnyPlaying)
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
            if (open && Input.GetKeyDown(KeyCode.Escape))
            {
                if (skillUi == null || !skillUi.CloseConfirm()) SetOpen(false);   // Esc сначала закрывает подтверждение сброса навыков
            }
            if (open) Hotkeys();
        }
        else if (open) SetOpen(false);   // разговор начался: окно закрываем

        // плавное открытие
        openK = Mathf.MoveTowards(openK, open ? 1f : 0f, Time.unscaledDeltaTime / 0.18f);
        bool show = openK > 0.001f;
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (show)
        {
            overlay.alpha = openK;
            panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, UiDraw.EaseOutCubic(openK));
            if (Time.unscaledTime >= refreshAt) Refresh();
            Animate();
        }

        UpdateHudButton();
    }

    void Hotkeys()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        for (int i = 0; i < 6; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                if (shift) UseFreePoint(i); else Buy(i);
            }
    }

    // ------------------------------------------------------------ действия
    void Toggle()
    {
        if (!unlocked) { toastUntil = Time.unscaledTime + 2f; return; }
        SetOpen(!open);
    }

    void SetOpen(bool v)
    {
        if (v && !unlocked) return;
        bool wasOpen = open;
        open = v;
        if (wasOpen && !v) SaveSystem.RequestSave("upgrades");   // окно закрыли: улучшения уходят в файл
        if (!v && skillUi != null) skillUi.CloseConfirm();
        if (v) { HasOpened = true; Refresh(); }
        else if (tempHighlight) { highlight = -1; tempHighlight = false; }
    }

    bool Allowed(int type) { return only < 0 || only == type; }

    void Buy(int type)
    {
        if (!FindRefs() || !Allowed(type)) return;
        if (up.TryBuy((UpgradeType)type)) { flashRow = type; flashUntil = Time.unscaledTime + 0.5f; }
        Refresh();
    }

    void UseFreePoint(int type)
    {
        if (!FindRefs() || !Allowed(type)) return;
        if (up.TryUseFreePoint((UpgradeType)type)) { flashRow = type; flashUntil = Time.unscaledTime + 0.5f; }
        Refresh();
    }

    // ------------------------------------------------------------ обновление
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    void Refresh()
    {
        refreshAt = Time.unscaledTime + 0.1f;
        if (!built) return;
        bool ok = FindRefs();

        Set(coinsText, (inv != null ? inv.coinsCount : 0).ToString());
        Set(pointsText, (up != null ? up.freePoints : 0).ToString());

        RefreshRows(ok);
        if (skillUi != null) skillUi.Refresh();   // монеты и очки могли измениться
    }

    void RefreshRows(bool ok)
    {
        for (int i = 0; i < 6; i++)
        {
            var r = rows[i];
            var t = (UpgradeType)i;
            int lvl = ok ? up.Level(t) : 0;
            int max = GameBalance.UpgradeMaxLevel[i];
            bool isMax = lvl >= max;
            bool lockedType = ok && !up.IsUnlocked(t);
            bool allowed = Allowed(i);

            Set(r.name, "<color=#6DBE6A>" + (i + 1) + "</color>  " + Names[i]);

            float now = ok ? up.ValueAt(t, lvl) : PlayerUpgrades.Calc(t, lvl, TalentBonus.None);
            string line = Fmt(i, now);
            if (!isMax)
            {
                float next = ok ? up.ValueAt(t, lvl + 1) : PlayerUpgrades.Calc(t, lvl + 1, TalentBonus.None);
                line += "  →  <color=#6DBE6A>" + Fmt(i, next) + "</color>";
            }
            Set(r.desc, line);

            for (int k = 0; k < r.pips.Length; k++) r.pips[k].color = k < lvl ? UiKit.Brass : new Color32(0x1E, 0x2B, 0x3D, 255);

            // цена и кнопка
            bool showPrice = !isMax && !lockedType;
            r.priceGo.SetActive(showPrice);
            string label;
            bool canBuy = false;
            if (isMax) label = "Макс";
            else if (lockedType) label = "С " + GameBalance.SawUnlockLevel + " ур.";
            else if (!allowed) label = "Позже";
            else
            {
                int cost = GameBalance.UpgradeCost(i, lvl);
                canBuy = ok && inv.coinsCount >= cost;
                label = "Купить";
            }
            if (showPrice)
            {
                int cost = GameBalance.UpgradeCost(i, lvl);
                bool afford = ok && inv.coinsCount >= cost;
                Set(r.price, cost.ToString());
                r.price.color = afford ? UiKit.BrassHi : Bad;
            }
            Set(r.buyTxt, label);
            r.buyTxt.color = canBuy ? UiKit.Navy : new Color(0.86f, 0.88f, 0.92f, 1f);
            r.buy.interactable = canBuy;

            // бесплатное очко
            r.ptGo.SetActive(ok && up.freePoints > 0 && !isMax && !lockedType && allowed);
        }
    }

    // ------------------------------------------------------------ анимация подсветок
    void Animate()
    {
        float t = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);

        for (int i = 0; i < 6; i++)
        {
            var g = rows[i].glow;
            bool flash = i == flashRow && t < flashUntil;
            bool hl = i == highlight;
            g.color = flash ? new Color(UiKit.BrassHi.r, UiKit.BrassHi.g, UiKit.BrassHi.b, 0.95f)
                    : hl ? new Color(UiKit.Good.r, UiKit.Good.g, UiKit.Good.b, 0.35f + 0.55f * pulse)
                    : Clear;
        }
    }

    // ------------------------------------------------------------ кнопка на HUD
    void UpdateHudButton()
    {
        float t = Time.unscaledTime;
        bool pulse = unlocked && !open && highlight >= 0;

        UiBtn style;
        int pts = 0;
        if (!unlocked)
        {
            style = UiBtn.Gray;
            hudText.color = new Color(0.86f, 0.88f, 0.92f, 1f);
            hudText.text = t < toastUntil ? "Откроется по сюжету" : "Прокачка\n<size=60%>закрыто</size>";
        }
        else
        {
            style = pulse ? UiBtn.Green : UiBtn.Amber;
            pts = (FindRefs() ? up.freePoints : 0) + PlayerSkills.FreePoints;   // очки улучшений и очки навыков вместе
            hudText.color = pulse ? Color.white : UiKit.Navy;
            hudText.text = "Прокачка";      // клавиша U нарисована отдельной клавишей справа, свободные очки отдельным значком
        }

        // значок слева: замок, пока закрыто, потом стрелка вверх
        UiIcon want = unlocked ? UiIcon.ArrowUp : UiIcon.Lock;
        if (hudIcon != null && want != hudIconShown) { hudIconShown = want; hudIcon.sprite = UiKit.Icon(want); }

        // значок свободных очков: зелёный кружок с числом, чуть пульсирует
        if (hudPtsRt != null)
        {
            bool showPts = pts > 0;
            if (hudPtsRt.gameObject.activeSelf != showPts) hudPtsRt.gameObject.SetActive(showPts);
            if (showPts)
            {
                hudPtsText.text = pts > 99 ? "99+" : pts.ToString();
                hudPtsRt.localScale = Vector3.one * (1f + 0.10f * Mathf.Sin(t * 5f));
            }
        }
        if (!hudStyleSet || style != hudStyle)
        {
            hudStyle = style;
            hudStyleSet = true;
            hudImg.sprite = UiKit.BtnSprite(style, style == UiBtn.Gray ? UiBtnState.Disabled : UiBtnState.Normal);
        }
        float s = pulse ? 1f + 0.04f * Mathf.Sin(t * 5f) : 1f;
        hudImg.rectTransform.localScale = Vector3.one * s;
    }

    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        if (built) return;

        var cgo = new GameObject("UpgradeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // --- кнопка на HUD
        hudImg = UiKit.MakeImage(cgo.transform, "HudButton", UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Disabled), true, 1f);
        Anch(hudImg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-HudRight, HudBottom), new Vector2(HudBtnW, HudBtnH));
        AddBtn(hudImg, Toggle);

        // слева круглый значок, справа клавиша (на телефоне клавиатуры нет, тогда текст занимает всё место)
        var med = UiKit.MakeImage(hudImg.transform, "IconMedallion", UiKit.Medallion, false);
        Anch(med.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 1f), new Vector2(60f, 60f));
        hudIcon = UiKit.MakeIcon(med.transform, UiIcon.Lock, 36f, "Icon");
        bool keyboard = !Application.isMobilePlatform;

        hudText = UiKit.AddButtonLabel(hudImg.transform, "Прокачка", UiBtn.Amber, 32f);
        hudText.alignment = TextAlignmentOptions.Left;
        hudText.rectTransform.offsetMin = new Vector2(84f, 6f);
        hudText.rectTransform.offsetMax = new Vector2(keyboard ? -62f : -14f, -6f);
        AutoSize(hudText, 16f, 30f);

        if (keyboard)
        {
            var key = UiKit.MakeKeycap(hudImg.transform, "U", 42f);
            Anch(key.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 1f), new Vector2(42f, 42f));
        }

        // «свободные очки»: зелёный кружок в правом верхнем углу
        hudPts = UiKit.MakeImage(hudImg.transform, "PointsBadge", UiKit.Medallion, false);
        hudPts.color = UiKit.Good;
        hudPtsRt = hudPts.rectTransform;
        Anch(hudPtsRt, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6f, -4f), new Vector2(38f, 38f));
        hudPtsText = UiKit.MakeLabel(hudPts.transform, "0", 22f, Color.white, TextAlignmentOptions.Center, true);
        Stretch(hudPtsText.rectTransform);
        hudPtsRt.gameObject.SetActive(false);

        // --- затемнение: только гасит клики по миру
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // --- окно: деревянная планка сверху, внутри две панели
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        win.raycastTarget = true;                                // клик по окну не закрывает его
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);

        var left = UiKit.MakePanel(content, new Vector2(LeftW, PanelH), false, 0.6f, "Upgrades");
        TL(left.rectTransform, 0, 0, LeftW, PanelH);
        for (int i = 0; i < 6; i++) rows[i] = BuildRow(i, left.transform);

        // справа вместо старых талантов: дерево навыков (SkillTreeUi сам строит содержимое в панели 682 x 807)
        var right = UiKit.MakePanel(content, new Vector2(RightW, PanelH), false, 0.6f, "Skills");
        TL(right.rectTransform, LeftW + 20f, 0, RightW, PanelH);
        skillUi = gameObject.AddComponent<SkillTreeUi>();
        skillUi.Build(right.transform, RightW, PanelH);

        var hint = UiKit.MakeLabel(ov.transform, "1-6 купить   ·   Shift+1-6 свободное очко   ·   U или Esc закрыть", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(1000, 34));

        ov.gameObject.SetActive(false);
        built = true;
    }

    void BuildHeader()
    {
        // монеты и очки слева на деревянной планке
        var coin = UiKit.MakeIcon(panel, UiIcon.Coin, 36f, "CoinIcon");
        TLC(coin.rectTransform, 64, 27, 36, 36);
        coinsText = UiKit.MakeLabel(panel, "0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(coinsText.rectTransform, 90, 8, 150, 38);

        var star = UiKit.MakeIcon(panel, UiIcon.Star, 36f, "PointsIcon");
        TLC(star.rectTransform, 268, 27, 36, 36);
        pointsText = UiKit.MakeLabel(panel, "0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(pointsText.rectTransform, 294, 8, 100, 38);

        // заголовок по центру
        var axe = UiKit.MakeIcon(panel, UiIcon.Axe, 40f, "TitleIcon");
        TLC(axe.rectTransform, WinW * 0.5f - 104f, 27, 40, 40);
        var title = UiKit.MakeLabel(panel, "ПРОКАЧКА", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(20, -8), new Vector2(260, 40));

        // закрыть
        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.onClick.AddListener(() => SetOpen(false));
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    // ------------------------------------------------------------ левая панель: строка прокачки
    Row BuildRow(int i, Transform parent)
    {
        var r = new Row();
        float y = RowTop + i * (RowH + RowGap);
        float x = (LeftW - RowW) * 0.5f;

        r.glow = UiKit.MakeImage(parent, "Glow" + i, UiKit.Slot, true, 1f);
        r.glow.color = Clear;
        TL(r.glow.rectTransform, x - 10, y - 10, RowW + 20, RowH + 20);

        r.bg = UiKit.MakeImage(parent, "Row" + i, UiKit.Slot, true, 1f);
        TL(r.bg.rectTransform, x, y, RowW, RowH);
        var row = r.bg.transform;

        // иконка в рамке
        var slot = UiKit.MakeSlot(row, 90f, 1f, "IconSlot");
        Anch(slot.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(90, 90));
        UiKit.MakeIcon(slot.transform, RowIcons[i], 60f);

        // название и «до → после»
        r.name = UiKit.MakeLabel(row, "", 24f, Color.white, TextAlignmentOptions.Left, true);
        TL(r.name.rectTransform, 118, 12, 228, 32);
        AutoSize(r.name, 14f, 24f);
        r.desc = UiKit.MakeLabel(row, "", 20f, Soft, TextAlignmentOptions.Left, false);
        TL(r.desc.rectTransform, 118, 46, 228, 28);
        AutoSize(r.desc, 13f, 20f);

        // уровень точками
        int max = GameBalance.UpgradeMaxLevel[i];
        r.pips = new Image[max];
        for (int k = 0; k < max; k++)
        {
            var ring = UiKit.MakeImage(row, "Pip", Dot, false);
            ring.color = UiKit.Ink;
            Anch(ring.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(118 + k * 28, 16), new Vector2(22, 22));
            var fill = UiKit.MakeImage(ring.transform, "Fill", Dot, false);
            fill.rectTransform.sizeDelta = new Vector2(16, 16);
            r.pips[k] = fill;
        }

        // цена (монета + число)
        r.priceGo = NewRT("Price", row).gameObject;
        var pr = (RectTransform)r.priceGo.transform;
        TR(pr, 14, 14, 140, 34);
        var coin = UiKit.MakeIcon(pr, UiIcon.Coin, 32f);
        Anch(coin.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(32, 32));
        r.price = UiKit.MakeLabel(pr, "", 28f, UiKit.BrassHi, TextAlignmentOptions.Left, true);
        r.price.rectTransform.offsetMin = new Vector2(40, 0);
        r.price.rectTransform.offsetMax = Vector2.zero;

        // кнопка покупки
        r.buy = UiKit.MakeButton(row, "Купить", UiBtn.Amber, new Vector2(140, 54), 24f, 1f);
        Anch(r.buy.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 12), new Vector2(140, 54));
        r.buyTxt = r.buy.GetComponentInChildren<TMP_Text>();
        int idx = i;
        r.buy.onClick.AddListener(() => Buy(idx));

        // кнопка «свободное очко»
        r.pt = UiKit.MakeButton(row, "+1", UiBtn.Yellow, new Vector2(56, 54), 26f, 1f);
        Anch(r.pt.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14 - 140 - 8, 0), new Vector2(56, 54));
        r.ptGo = r.pt.gameObject;
        r.pt.onClick.AddListener(() => UseFreePoint(idx));

        return r;
    }

    // ------------------------------------------------------------ мелкие помощники
    static Sprite dot;

    // круг для точек уровня
    static Sprite Dot
    {
        get
        {
            if (dot != null) return dot;
            const int n = 32;
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
            dot.name = "UpgradeWindow_Dot";
            return dot;
        }
    }

    static void AddBtn(Image img, UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(a);
    }

    // от правого верхнего угла
    static void TR(RectTransform r, float right, float top, float w, float h)
    {
        Anch(r, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-right, -top), new Vector2(w, h));
    }
}
