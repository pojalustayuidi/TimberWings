// Assets/_Project/Scripts/UI/Pets/PetCrateWindow.cs (НЕ в Editor)
// Окно открытия клети питомцев в стиле UiKit (как UpgradeWindow и WorkersWindow). Всё строится из кода, в сцене ничего настраивать не нужно.
//   PetCrateWindow.Open(CrateType) открыть, PetCrateWindow.Close() закрыть, PetCrateWindow.IsOpen открыто ли.
// Четыре экрана (четвёртый, «Что внутри?», открывается кнопкой на экране 1: все питомцы клети по редкостям с шансом на вид, прокрутка, описание по нажатию):
//   1. Подготовка: слева ящик с ореолом цвета клети и искрами, цена («Бесплатно» для первой клети); справа шансы по редкостям
//      (камень, полоса, проценты), полоса «До гарантии Редкого», кнопка «Открыть» (серая с жёлтой причиной, если нельзя).
//   2. Рулетка: PetManager.TryOpenCrate вызывается ОДИН раз по кнопке, результат известен сразу. Лента из 40 карточек едет и замедляется
//      (ease-out, 3,6 с), точно останавливается на выпавшей. Карточка под указателем крупнее, края ленты гаснут, под лентой имя живьём,
//      к концу указатель наливается цветом редкости. Тик через SfxManager («ui»). E или Esc пропускают рулетку.
//   3. Выпадение: карточка переворачивается со вспышкой цвета редкости, взрыв искр, вращающиеся лучи (Необычный и выше), тряска окна
//      (Редкий и Легендарный), тег «НОВЫЙ!», строка стата с иконкой, кнопки «Взять с собой» (PetManager.Equip), «В коллекцию», «Открыть ещё».
// Вся анимация короче 6 секунд. Клавиши: E или Esc закрыть (на рулетке пропустить), Enter или Пробел открыть клеть на экране 1.
// Пока окно открыто, MobileInput.ModalOpen = true: персонаж стоит, рубка и E «сквозь» окно не работают.
// Карточки, цвета редкости, иконки статов и спрайты эффектов лежат в PetUi.cs (там же PetCardView для будущего окна коллекции).
// 3D-питомца окно не показывает: иконка PetData.icon, а если её нет, лапка UiIcon.Paw на цветной плашке.
// Для теста в Editor: F9 открывает окно Лесной клети.
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

// ----- КАРТА ФАЙЛОВ (класс разбит на части partial, поведение не менялось) -----
// PetCrateWindow.cs: статический API, размеры, цвета, состояние, жизненный цикл (этот файл)
// PetCrateWindow.Screens.cs: частицы и экраны клети: подготовка, рулетка, выпадение
// PetCrateWindow.Update.cs: покадровое обновление окна
// PetCrateWindow.Build.cs: построение окна из кода: экраны 1-4 и «Шансы», помощники
public partial class PetCrateWindow : MonoBehaviour
{
    // ============================================================ статический API
    static PetCrateWindow instance;

    public static bool IsOpen { get { return instance != null && instance.open; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

#if UNITY_EDITOR
    // в Editor окно создаётся заранее, чтобы работал тестовый F9
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootForTests() { Ensure(); }
#endif

    static PetCrateWindow Ensure()
    {
        if (instance == null)
        {
            var go = new GameObject("PetCrateWindow");
            instance = go.AddComponent<PetCrateWindow>();
        }
        return instance;
    }

    public static void Open(CrateType crate) { Ensure().DoOpen(crate); }
    public static void Close() { if (instance != null) instance.DoClose(); }

    // Тест: сразу показать экран выпадения с заданным питомцем, без списания монет и без рулетки (см. PetDebugPanel)
    public static void DebugPreview(CrateType c, PetData d)
    {
        if (d == null) return;
        var w = Ensure();
        if (!w.open) w.DoOpen(c);
        if (!w.open) return;
        w.result = new PetInstance { uid = -1, data = d };
        w.ShowResult();
    }

    // ============================================================ размеры и тайминги (единицы Canvas 1920x1080)
    enum Step { Prepare, Roll, Result, Contents, Odds }

    const float WinW = 1100f, WinH = 720f, BS = 1.5f;
    const float InW = WinW - 78f;                 // внутренняя область 1022 x 627
    const float InH = WinH - 39f - 54f;

    const int CardCount = 40, WinIndex = 32, StartIndex = 2;
    const float CardW = 180f, CardH = 210f, Pitch = 196f;
    const float ViewW = 960f, ViewH = 250f, MaskInset = 8f;
    const float MaskCenter = (ViewW - 2f * MaskInset) * 0.5f;

    const float RollTime = 3.6f, HoldTime = 0.35f, FlipTime = 0.5f, FlashTime = 0.7f, IntroTime = 0.25f;   // сумма меньше 6 секунд

    // экран 1: ящик слева
    const float CrateCx = 210f, CrateCy = 225f;
    // экран 3: карточка по центру сверху
    const float CardCx = InW * 0.5f, CardCy = 150f;
    // экран 2: центр указателя в координатах окна
    const float PointerCy = 150f + ViewH * 0.5f;
    // кнопки экрана 3
    const float ResBtnW = 300f, ResBtnGap = 20f, ResBtnY = 508f, ResBtnH = 90f;

    // ============================================================ цвета
    static readonly Color Soft = PetUi.Soft;
    static readonly Color Dim = PetUi.Dim;
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color OffText = new Color(0.86f, 0.88f, 0.92f, 1f);
    static readonly Color Clear = PetUi.Clear;
    static readonly Color SlotDark = new Color32(0x0F, 0x18, 0x24, 255);

    static Color RarityFill(PetRarity r) { return PetUi.RarityFill(r); }
    static Color RarityAccent(PetRarity r) { return PetUi.RarityAccent(r); }
    static string Hex(Color c) { return PetUi.Hex(c); }

    // ============================================================ частицы (искры)
    class Spark
    {
        public RectTransform rt;
        public Image img;
        public bool alive;
        public float x, y, vx, vy, g, life, max, s0, s1, rot, vrot;
        public Color col;
    }

    // ============================================================ состояние
    bool open, built;
    int openFrame = -1, closeFrame = -1;
    float openK, refreshAt;
    CrateType crate;
    Step step;

    PetInstance result;
    PetData resultData;
    float rollT, resultT, flashT, stripX0, stripX1, lastTickTime;
    int lastTickIdx;
    bool flashed;
    float shakeT, shakeAmp, ambientT;

    PlayerInventory inv;

    CanvasGroup overlay;
    RectTransform panel, stripRt, fx;
    TMP_Text titleText, coinsText, hintText;
    GameObject prepareGo, rollGo, resultGo;
    Image screenFlash;
    readonly Spark[] sparks = new Spark[72];

    // экран 1
    TMP_Text priceText, guaranteeText, reasonText, openLabel;
    Image crateImg, crateHalo, priceCoin, pityFill;
    Button openBtn;
    int priceLayout = -1;                          // 0 цена с монетой, 1 «Бесплатно»
    readonly GameObject[] oddsRow = new GameObject[4];
    readonly Image[] oddsGem = new Image[4];
    readonly Image[] oddsBar = new Image[4];
    readonly TMP_Text[] oddsName = new TMP_Text[4];
    readonly TMP_Text[] oddsPct = new TMP_Text[4];

    // экран 2
    TMP_Text rollTitle, liveName;
    CanvasGroup rollCg;
    readonly PetCardView[] stripCards = new PetCardView[CardCount];
    readonly PetData[] stripData = new PetData[CardCount];
    readonly List<Image> pointerBars = new List<Image>();
    readonly List<PetData> tmpPets = new List<PetData>();

    // экран 3
    PetCardView resultCard;
    GameObject backGo;
    Image cardFlash, burst, glowIdle, rays, gemL, gemR, statBadge, statBadgeIcon, newTag;
    CanvasGroup resultInfo;
    TMP_Text nameText, rarityText, statText, dupText, slotNote, equipLabel, newTagLabel;
    Button equipBtn;
    bool isNew;

    // экран 1 (анимация появления)
    float prepT;
    Image crateRays;
    readonly RectTransform[] oddsRt = new RectTransform[4];
    readonly CanvasGroup[] oddsCg = new CanvasGroup[4];
    readonly float[] oddsTarget = new float[4];

    // экран 2 (подсветка редкости под указателем)
    TMP_Text liveRarity;
    RectTransform pointerRt;
    float pointerK, pingT, popT, lastPingTime;
    PetRarity liveR;

    // экран 4: что может выпасть
    GameObject contentsGo;
    RectTransform contentsRt;

    // экран 5: таблица шансов всех клетей
    GameObject oddsGo;
    TMP_Text detailName, detailStat;
    Image detailBadgeIcon, detailGem;
    PetCardView selectedCard;
    readonly List<PetCardView> contentCards = new List<PetCardView>();

    // ============================================================ жизненный цикл
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void DoOpen(CrateType c)
    {
        if (Time.frameCount == closeFrame) return;        // E, которой закрыли окно, его же не откроет заново
        if (!open && (MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying)) return;   // два окна сразу не нужны

        EnsureBuilt();
        if (!built) return;
        crate = c;
        open = true;
        openFrame = Time.frameCount;                      // E, которой открыли окно, его же не закроет
        result = null;
        resultData = null;
        shakeT = 0f;
        ambientT = 0f;
        priceLayout = -1;
        ClearSparks();
        SetStep(Step.Prepare);
        Refresh();
    }

    void DoClose()
    {
        if (!open) return;
        open = false;
        closeFrame = Time.frameCount;
    }

    void EnsureBuilt()
    {
        if (built) return;
        UiInputGuard.EnsureEventSystem();
        Build();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterModal() { UiModal.Register(typeof(PetCrateWindow), () => IsOpen, Close); }

    static bool OtherWindowOpen()
    {
        return UiModal.AnyOpen(typeof(PetCrateWindow));   // реестр окон: UiModal
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (open) DoClose(); else DoOpen(CrateType.Forest);
        }
#endif
        if (!built) return;

        float dt = Time.unscaledDeltaTime;

        if (open)
        {
            // начался разговор или открылось другое окно: закрываемся (если рулетка шла, питомец уже в коллекции)
            if (DialogueManager.IsBusy || DialogueSequence.AnyPlaying || OtherWindowOpen())
                DoClose();
            else
            {
                // выбранная кнопка реагировала бы ещё и на Enter: снимаем выделение, Enter обрабатывает только Hotkeys
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                Hotkeys();
            }
        }

        openK = Mathf.MoveTowards(openK, open ? 1f : 0f, dt / 0.15f);
        bool show = openK > 0.001f;
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (!show) return;

        overlay.alpha = openK;
        panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, UiDraw.EaseOutCubic(openK));

        // тряска окна при редком дропе
        Vector2 sh = Vector2.zero;
        if (shakeT > 0f)
        {
            shakeT -= dt;
            float k = Mathf.Clamp01(shakeT / 0.45f);
            sh = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shakeAmp * k;
        }
        panel.anchoredPosition = sh;

        if (!open) return;
        if (Time.unscaledTime >= refreshAt) Refresh();
        Animate(dt);
    }

    void Hotkeys()
    {
        if (Time.frameCount == openFrame) return;
        bool esc = Input.GetKeyDown(KeyCode.Escape);
        bool act = Input.GetKeyDown(MobileInput.ActionKey);

        if (step == Step.Roll)
        {
            if (esc || act) SkipRoll();
            return;
        }
        if (step == Step.Contents)
        {
            if (esc || act) CloseContents();
            return;
        }
        if (step == Step.Odds)
        {
            if (esc || act) CloseOdds();
            return;
        }
        if (esc || act) { DoClose(); return; }

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        if (enter && step == Step.Prepare && openBtn != null && openBtn.interactable) StartRoll();
    }

    // ============================================================ действия
    void OnOverlayClick()
    {
        if (step != Step.Roll) DoClose();       // во время рулетки клик мимо окна её не прерывает
    }

    void OnCloseButton()
    {
        if (step == Step.Roll) SkipRoll();
        else if (step == Step.Contents) CloseContents();
        else if (step == Step.Odds) CloseOdds();
        else DoClose();
    }

    void StartRoll()
    {
        if (step != Step.Prepare) return;
        var pm = PetManager.Instance;
        if (pm == null) { RefreshPrepare(); return; }

        PetInstance inst;
        PetOpenResult r = pm.TryOpenCrate(crate, out inst);        // монеты списываются здесь, один раз
        if (r != PetOpenResult.Ok || inst == null || inst.data == null)
        {
            Refresh();                                              // причина покажется на экране 1
            return;
        }

        result = inst;
        BuildStrip(inst.data, pm.database);
        rollT = 0f;
        lastTickIdx = StartIndex;
        lastTickTime = 0f;
        stripRt.anchoredPosition = new Vector2(stripX0, 0f);
        UpdateStripLook(stripX0);
        pointerK = 0f; pingT = 0f; popT = 0f;
        UpdateLive(StartIndex, false);
        rollCg.alpha = 0f;
        SetPointerColor(UiKit.Brass);
        Set(rollTitle, PetText.Crate(crate) + "…");
        SetStep(Step.Roll);
        SfxManager.Play("hint", 0.6f, 1f);

        // «хлопок» открытия: искры у кнопки и лёгкий толчок окна
        Color ca = PetUi.CrateAccent(crate);
        Burst(InW * 0.5f, 554f, 16, UiKit.BrassHi, 160f, 420f);
        Burst(CrateCx, CrateCy, 14, ca, 140f, 380f);
        shakeAmp = 4f; shakeT = 0.25f;
        Refresh();
    }

    void SkipRoll()
    {
        if (step != Step.Roll) return;
        stripRt.anchoredPosition = new Vector2(stripX1, 0f);
        UpdateStripLook(stripX1);
        ShowResult();
    }

    void OnEquip()
    {
        var pm = PetManager.Instance;
        if (pm == null || result == null) return;
        if (pm.Equip(result.uid))
        {
            SfxManager.Play("coin", 0.7f, 1.1f);
            float bx = (InW - (ResBtnW * 3f + ResBtnGap * 2f)) * 0.5f;
            Burst(bx + ResBtnW * 0.5f, ResBtnY + ResBtnH * 0.5f, 14, UiKit.BrassHi, 120f, 360f);
        }
        RefreshResultButtons();
    }

    void OnAgain()
    {
        if (step != Step.Result) return;
        SetStep(Step.Prepare);
        Refresh();
    }

    void SetStep(Step s)
    {
        step = s;
        prepareGo.SetActive(s == Step.Prepare);
        rollGo.SetActive(s == Step.Roll);
        resultGo.SetActive(s == Step.Result);
        contentsGo.SetActive(s == Step.Contents);
        oddsGo.SetActive(s == Step.Odds);
        if (s == Step.Prepare) prepT = 0f;
        switch (s)
        {
            case Step.Prepare: Set(hintText, "Enter — открыть   ·   E или Esc — закрыть"); break;
            case Step.Roll: Set(hintText, "E или Esc — пропустить"); break;
            case Step.Contents: Set(hintText, "Колёсико или перетаскивание — прокрутка   ·   E или Esc — назад"); break;
            case Step.Odds: Set(hintText, "E или Esc — назад"); break;
            default: Set(hintText, "E или Esc — закрыть"); break;
        }
    }

}
