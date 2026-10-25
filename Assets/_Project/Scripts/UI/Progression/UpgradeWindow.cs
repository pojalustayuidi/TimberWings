// Assets/_Project/Scripts/UI/Progression/UpgradeWindow.cs (НЕ в Editor)
// Окно прокачки (GDD раздел 3) в стиле Candy. Всё строится из кода, ничего настраивать в сцене не нужно (StoryFlow создаёт его сам).
//   Окно: UiCandy.Window (облачная панель, лавандовая лента, «×», CandyPopIn). Монеты и очки на ленте чипами. Затемнение мягкий индиго.
//   Кнопка «Прокачка» внизу справа: мятно-персиковая желейная (CandyButtonJuice), пока сюжет её не открыл, серая с подписью «закрыто».
//   Слева: 6 прокачек (сила удара, скорость бега, рюкзак, двойная добыча, торг, пила): уровень мятными точками, «до → после», цена чипом с монетой (UiCandy.Chip), мятная кнопка «Купить».
//   Недоступная кнопка серая и на клик «мотает головой» (это делает CandyButtonJuice). Весь текст индиго, белого и бледного нет.
//   Покупка сыплет искры CandyFx из кнопки строки (нужен CandyFxKit, UiCandy.Kit находит его сам; без него искр нет).
//   Справа: дерево навыков за очки уровня (SkillTreeUi, логика в PlayerSkills): 3 ветки × 3 яруса на вымпелах, клик по узлу показывает карточку внизу.
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
    static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    const string HexDone = "#2A9573";   // UiCandy.Done: цифра и «после» в строке прокачки
    // затемнение за окном: мягкий индиго вместо чёрного (как у окна стройки)
    static readonly Color Dimmer = new Color(0.17f, 0.18f, 0.36f, 0.4f);

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float WinW = 1400f, WinH = 900f;                   // окно (рамку и ленту рисует UiCandy.Window)
    const float LeftW = 620f, RightW = 682f, PanelH = 807f;  // рабочая область 1322 x 807 по центру содержимого окна
    const float RowW = 570f, RowH = 118f, RowGap = 10f, RowTop = 22f;

    class Row
    {
        public Image glow, bg;
        public TMP_Text name, desc, buyTxt;
        public UiCandy.ChipRefs priceChip;   // цена: чип с монетой
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
    RectTransform panel;              // окно UiCandy.Window (на нём CandyPopIn: его масштаб здесь не трогаем)
    Image hudImg;
    CandyButtonJuice hudJuice;        // «желе» на кнопке HUD: пружина, искры, качается, когда есть что открыть
    TMP_Text hudText, coinsText, pointsText;
    readonly Row[] rows = new Row[6];
    SkillTreeUi skillUi;   // правая панель: дерево навыков

    PlayerUpgrades up;
    PlayerInventory inv;

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { Instance = this; UiModal.Register(this, () => IsOpen); }
    void OnDestroy() { UiModal.Unregister(this); if (Instance == this) Instance = null; }

    void Start()
    {
        EnsureEventSystem();
        Build();
        Refresh();
    }

    static void EnsureEventSystem() { UiInputGuard.EnsureEventSystem(); }

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
            overlay.alpha = openK;   // «выскакивание» окна делает CandyPopIn: включение затемнения запускает его заново
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

    // искры из кнопки строки при удачной покупке (без CandyFxKit ничего не происходит)
    void Sparks(int type)
    {
        if (type < 0 || type >= rows.Length || rows[type] == null) return;
        var at = rows[type].buy.GetComponent<RectTransform>();
        var kit = UiCandy.Kit;
        CandyFx.Burst(kit, at, new Color(1f, 0.92f, 0.55f, 1f), 10, 260f);
        CandyFx.Ring(kit, at, new Color(1f, 1f, 1f, 0.9f));
    }

    void Buy(int type)
    {
        if (!FindRefs() || !Allowed(type)) return;
        if (up.TryBuy((UpgradeType)type)) { flashRow = type; flashUntil = Time.unscaledTime + 0.5f; Sparks(type); }
        Refresh();
    }

    void UseFreePoint(int type)
    {
        if (!FindRefs() || !Allowed(type)) return;
        if (up.TryUseFreePoint((UpgradeType)type)) { flashRow = type; flashUntil = Time.unscaledTime + 0.5f; Sparks(type); }
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

            Set(r.name, "<color=" + HexDone + ">" + (i + 1) + "</color>  " + Names[i]);
            r.name.color = lockedType ? UiCandy.Muted : UiCandy.Ink;
            // состояние читается по цвету карточки: достигнут максимум мятная, не открыто лавандово-серая
            r.bg.color = UiCandy.TintOf(isMax ? UiTone.Mint : lockedType ? UiTone.Locked : UiTone.Neutral);

            float now = ok ? up.ValueAt(t, lvl) : PlayerUpgrades.Calc(t, lvl, TalentBonus.None);
            string line = Fmt(i, now);
            if (!isMax)
            {
                float next = ok ? up.ValueAt(t, lvl + 1) : PlayerUpgrades.Calc(t, lvl + 1, TalentBonus.None);
                line += "  →  <color=" + HexDone + ">" + Fmt(i, next) + "</color>";
            }
            Set(r.desc, line);

            // уровень: мятные точки заполнены, пустые светло-лавандовые
            for (int k = 0; k < r.pips.Length; k++) r.pips[k].color = k < lvl ? UiCandy.Mint : UiCandy.TintOf(UiTone.Locked);

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
                Set(r.priceChip.label, cost.ToString());
                r.priceChip.label.color = afford ? UiCandy.GoldText : UiCandy.Danger;
                r.priceChip.bg.color = UiCandy.TintOf(afford ? UiTone.Peach : UiTone.Danger);
            }
            Set(r.buyTxt, label);
            r.buyTxt.color = canBuy ? UiCandy.Ink : UiCandy.Muted;   // недоступная кнопка серая, подпись приглушённый индиго
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
            g.color = flash ? new Color(UiCandy.Lemon.r, UiCandy.Lemon.g, UiCandy.Lemon.b, 0.95f)            // только что куплено: лимонная вспышка
                    : hl ? new Color(UiCandy.Mint.r, UiCandy.Mint.g, UiCandy.Mint.b, 0.35f + 0.55f * pulse)   // подсказка задания: мятное дыхание
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
            hudText.color = UiCandy.Muted;
            hudText.text = t < toastUntil ? "Откроется по сюжету" : "Прокачка\n<size=60%>закрыто</size>";
        }
        else
        {
            style = pulse ? UiBtn.Green : UiBtn.Amber;
            pts = (FindRefs() ? up.freePoints : 0) + PlayerSkills.FreePoints;   // очки улучшений и очки навыков вместе
            hudText.color = UiCandy.Ink;
            hudText.text = "Прокачка";      // клавиша U нарисована отдельной клавишей справа, свободные очки отдельным значком
        }

        // значок слева: замок, пока закрыто, потом стрелка вверх (значки берём в Candy: вне using по умолчанию был бы латунный)
        UiIcon want = unlocked ? UiIcon.ArrowUp : UiIcon.Lock;
        if (hudIcon != null && want != hudIconShown) { hudIconShown = want; hudIcon.sprite = UiKit.CandyIcon(want); }

        // значок свободных очков: мятный кружок с числом, чуть пульсирует
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
            using (UiKit.UseSkin(UiSkin.Candy)) hudImg.sprite = UiKit.BtnSprite(style, style == UiBtn.Gray ? UiBtnState.Disabled : UiBtnState.Normal);
            if (hudJuice != null)
            {
                hudJuice.fxColor = UiCandy.FxColor(style);
                if (hudJuice.kit == null) hudJuice.kit = UiCandy.Kit;   // набор искр мог найтись уже после сборки окна
            }
        }
        // готовая к нажатию кнопка раз в несколько секунд качается сама (CandyButtonJuice.attention).
        // Ручное «дыхание» масштабом убрано: оно мешало пружине CandyButtonJuice
        if (hudJuice != null) hudJuice.attention = pulse;
    }

    // ------------------------------------------------------------ сборка UI
    void Build()
    {
        if (built) return;
        using (UiKit.UseSkin(UiSkin.Candy)) BuildAll();   // всё окно собирается в скине Candy
        built = true;
    }

    void BuildAll()
    {
        var cgo = new GameObject("UpgradeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.WindowUpgrade;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // --- кнопка на HUD: конфетное «желе»
        hudImg = UiKit.MakeImage(cgo.transform, "HudButton", UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Disabled), true, 1f);
        Anch(hudImg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-HudRight, HudBottom), new Vector2(HudBtnW, HudBtnH));
        AddBtn(hudImg, Toggle);
        hudJuice = UiCandy.Juice(hudImg.gameObject, UiBtn.Gray);

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

        // «свободные очки»: мятный кружок в правом верхнем углу, число индиго
        hudPts = UiKit.MakeImage(hudImg.transform, "PointsBadge", UiKit.Medallion, false);
        hudPts.color = UiCandy.Mint;
        hudPtsRt = hudPts.rectTransform;
        Anch(hudPtsRt, new Vector2(1f, 1f), Mid, new Vector2(-6f, -4f), new Vector2(38f, 38f));
        hudPtsText = UiKit.MakeLabel(hudPts.transform, "0", 22f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        Stretch(hudPtsText.rectTransform);
        hudPtsRt.gameObject.SetActive(false);

        // --- затемнение: мягкий индиго, только гасит клики по миру
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = Dimmer;
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // --- окно: облачная панель с лавандовой лентой, «×» и CandyPopIn делает UiCandy.Window
        var win = UiCandy.Window(ov.transform, new Vector2(WinW, WinH), "ПРОКАЧКА", () => SetOpen(false), "Window");
        panel = win.root;
        if (win.close != null) win.close.navigation = new Navigation { mode = Navigation.Mode.None };   // Enter и стрелки крестик не жмут
        BuildHeader();

        // рабочая область 1322 x 807 по центру содержимого окна
        var body = NewRT("Body", win.content);
        Anch(body, Mid, Mid, Vector2.zero, new Vector2(LeftW + 20f + RightW, PanelH));

        // панели чуть темнее фона окна (лавандовые), чтобы белые карточки строк читались
        var left = UiKit.MakePanel(body, new Vector2(LeftW, PanelH), false, 0.6f, "Upgrades");
        left.color = UiCandy.TintOf(UiTone.Lavender);
        TL(left.rectTransform, 0, 0, LeftW, PanelH);
        for (int i = 0; i < 6; i++) rows[i] = BuildRow(i, left.transform);

        // справа дерево навыков (SkillTreeUi само строит содержимое в панели 682 x 807, тоже в Candy)
        var right = UiKit.MakePanel(body, new Vector2(RightW, PanelH), false, 0.6f, "Skills");
        right.color = UiCandy.TintOf(UiTone.Lavender);
        TL(right.rectTransform, LeftW + 20f, 0, RightW, PanelH);
        skillUi = gameObject.AddComponent<SkillTreeUi>();
        skillUi.Build(right.transform, RightW, PanelH);

        // подсказка клавиш: светлая пилюля под окном (на затемнении голый текст не читался бы)
        var hintBg = UiKit.MakeImage(ov.transform, "HintPill", UiKit.Slot, true, 0.7f);
        hintBg.color = new Color(1f, 1f, 1f, 0.92f);
        Anch(hintBg.rectTransform, Mid, new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 14f)), new Vector2(920, 46));
        UiKit.MakeLabel(hintBg.transform, "1-6 купить   ·   Shift+1-6 свободное очко   ·   U или Esc закрыть", 22f, UiCandy.Muted, TextAlignmentOptions.Center, false);

        ov.gameObject.SetActive(false);
    }

    // монеты и очки чипами слева на ленте, значок топора слева от названия (название и «×» делает UiCandy.Window)
    void BuildHeader()
    {
        const float chipH = 34f, chipTop = 10f;
        var coin = UiCandy.Chip(panel, UiIcon.Coin, "0", chipH, UiTone.Peach, 130f);
        TL(coin.rt, 30f, chipTop, 130f, chipH);
        coinsText = coin.label;

        var star = UiCandy.Chip(panel, UiIcon.Star, "0", chipH, UiTone.Lemon, 100f);
        TL(star.rt, 30f + 130f + 12f, chipTop, 100f, chipH);
        pointsText = star.label;

        var axe = UiCandy.Icon(panel, UiIcon.Axe, 40f, "TitleIcon");
        TLC(axe.rectTransform, WinW * 0.5f - 128f, 27f, 40f, 40f);
    }

    // ------------------------------------------------------------ левая панель: строка прокачки
    Row BuildRow(int i, Transform parent)
    {
        var r = new Row();
        float y = RowTop + i * (RowH + RowGap);
        float x = (LeftW - RowW) * 0.5f;
        const float rowBs = 0.8f;                  // как у UiCandy.Card такой высоты: голубая «юбка» снизу 12 px
        const float skirt = 15f * rowBs;

        // подсветка строки (задание, покупка): мягкая лужица за карточкой
        r.glow = UiKit.MakeImage(parent, "Glow" + i, UiKit.Slot, true, 1f);
        r.glow.color = Clear;
        TL(r.glow.rectTransform, x - 10, y - 10, RowW + 20, RowH + 20);

        // карточка-строка: цвет задаёт RefreshRows (белая, мятная на максимуме, серая, если не открыто)
        r.bg = UiKit.MakeImage(parent, "Row" + i, UiKit.Panel, true, rowBs);
        TL(r.bg.rectTransform, x, y, RowW, RowH);
        var row = r.bg.transform;

        // иконка в медальоне
        const float msz = 84f;
        var med = UiKit.MakeImage(row, "IconBack", UiKit.Medallion, false);
        Anch(med.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, skirt * 0.5f), new Vector2(msz, msz));
        var ic = UiKit.MakeIcon(med.transform, RowIcons[i], 54f);
        ic.rectTransform.anchoredPosition = Vector2.zero;

        // название и «до → после»
        r.name = UiKit.MakeLabel(row, "", 24f, UiCandy.Ink, TextAlignmentOptions.Left, true);
        TL(r.name.rectTransform, 112, 12, 236, 32);
        AutoSize(r.name, 14f, 24f);
        r.desc = UiKit.MakeLabel(row, "", 20f, UiCandy.Muted, TextAlignmentOptions.Left, false);
        TL(r.desc.rectTransform, 112, 46, 236, 28);
        AutoSize(r.desc, 13f, 20f);

        // уровень точками: индиго-кружок с мятной (или светло-лавандовой) заливкой
        int max = GameBalance.UpgradeMaxLevel[i];
        r.pips = new Image[max];
        for (int k = 0; k < max; k++)
        {
            var ring = UiKit.MakeImage(row, "Pip", Dot, false);
            ring.color = UiCandy.Ink;
            Anch(ring.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(112 + k * 28, skirt + 6f), new Vector2(22, 22));
            var fill = UiKit.MakeImage(ring.transform, "Fill", Dot, false);
            fill.rectTransform.sizeDelta = new Vector2(16, 16);
            r.pips[k] = fill;
        }

        // цена: чип с монетой (светлая пилюля, красный оттенок, если монет не хватает)
        r.priceChip = UiCandy.Chip(row, UiIcon.Coin, "0", 38f, UiTone.Peach);
        Anch(r.priceChip.rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -8), new Vector2(0, 38));
        r.priceGo = r.priceChip.bg.gameObject;

        // кнопка покупки: мятная желейная; недоступная серая и «мотает головой» на клик (CandyButtonJuice)
        r.buy = UiKit.MakeButton(row, "Купить", UiBtn.Green, new Vector2(140, 50), 24f, 1f);
        Anch(r.buy.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, skirt + 6f), new Vector2(140, 50));
        r.buyTxt = r.buy.GetComponentInChildren<TMP_Text>();
        int idx = i;
        r.buy.onClick.AddListener(() => Buy(idx));

        // кнопка «свободное очко»: лимонная
        r.pt = UiKit.MakeButton(row, "+1", UiBtn.Yellow, new Vector2(56, 50), 26f, 1f);
        Anch(r.pt.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14 - 140 - 8, skirt * 0.5f), new Vector2(56, 50));
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
