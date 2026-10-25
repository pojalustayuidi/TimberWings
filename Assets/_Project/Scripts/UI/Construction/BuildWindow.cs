// Assets/_Project/Scripts/UI/Construction/BuildWindow.cs (НЕ в Editor)
// Окно стройки в стиле Candy (UiCandy.Window, Chip, мятные полосы, желейные кнопки). Всё строится из кода, настраивать в сцене ничего не нужно:
// создаётся при первом E на площадке (BuildSiteInteractable).
//
// КАРТА ЧАСТЕЙ (partial class BuildWindow):
//   BuildWindow.cs        это файл: статика, размеры, состояние, жизненный цикл, Update, клавиши, действия (Сдать, Играть)
//   BuildWindow.Build.cs  сборка UI: Build, шапка, свёрнутая полоска, левая карточка, узлы стадий, правая колонка, кнопки
//   BuildWindow.Fill.cs   обновление: Refresh, FillRow, полосы, анимация
//
//   Слева (у каждого здания своё): значок здания, стадии 1-2-3 узлами, «Стадия N из M», название этапа и его описание,
//          чипы «время этапа» и «награда в опыте».
//   Справа: «Нужно для этапа» (бревна, доски, монеты: чип «сдано +несёшь/нужно» и мятная полоса), строка состояния с подсказкой
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
//
// CANDY: окно выскакивает через CandyPopIn (он на панели UiCandy.Window и играет заново при каждом включении затемнения).
// Скин Candy включается на время сборки и на время тех мест, где спрайты берутся позже (RebuildNodes, значок здания в Refresh):
// using (UiKit.UseSkin(UiSkin.Candy)). Остальные окна пока латунные. В задаче 19 обёртки уйдут.
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class BuildWindow : MonoBehaviour
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

    // ------------------------------------------------------------ цвета для rich text (на светлых Candy-панелях)
    // белый и бледно-жёлтый текст старого стиля пропал бы на светлом поле, поэтому всё идёт индиго и тёмными оттенками
    const string HexInk = "#2B2F5C";      // UiCandy.Ink
    const string HexMuted = "#5E6A96";    // UiCandy.Muted
    const string HexDone = "#2A9573";     // UiCandy.Done
    const string HexGold = "#9A5F00";     // UiCandy.GoldText (число награды, «Строим…», нехватка)

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080)
    const float WinW = 1120f, WinH = 800f;                  // окно целиком (с лентой-заголовком и «юбкой»)
    const float ContentW = 1042f, ContentH = 707f;         // рабочая область внутри окна (Body по центру UiCandy.Window.content)
    const float LeftW = 400f, RightX = 420f, RightW = 622f;
    const float NeedH = 390f, StatusY = 404f, StatusH = 130f, BtnY = 560f, BtnH = 112f;
    const float NodesW = 340f;                              // ширина ряда стадий в левой карточке
    const float RowW = 574f, RowH = 76f, RowStep = 88f, RowTop = 76f;
    const float ChipW = 214f, BarW = 346f;                  // в строке ресурса: чип «сдано/нужно» и мятная полоса справа
    const float StripW = 340f, StripH = 72f, StripY = 70f;                    // компактная полоска внизу по центру (там же, где плашка BuildReveal)

    class Btn
    {
        public Button btn;
        public TMP_Text txt;
        public Color fg;
        public CandyButtonJuice juice;
    }

    // строка ресурса: чип (значок и «сдано +несёшь/нужно»), мятная полоса (сдано + несёшь с собой)
    class Row
    {
        public RectTransform root;
        public Image fill, preview, check, chip;
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
    RectTransform panel;              // окно UiCandy.Window (на нём CandyPopIn: его масштаб здесь не трогаем)
    RectTransform body;               // рабочая область внутри окна: на ней «подпрыгивание» при событиях
    Image headerIcon, bigIcon, barFill, statusCard;
    TMP_Text titleText, stageCaption, stageName, stageDesc, timeText, xpText;
    TMP_Text needTitle, backpackText, statusText, barText;
    RectTransform nodesRoot, chipXp, barFrame, headerIconRt;
    Image[] nodeRing = new Image[0], nodeFill = new Image[0], nodeCheck = new Image[0], links = new Image[0];
    TMP_Text[] nodeNum = new TMP_Text[0];
    RectTransform targetNode;
    Row rowWood, rowPlank, rowCoin;
    Btn deposit, play;
    bool canPlayNow;

    // свёрнутая полоска: название, полоса прогресса, «ещё N с»
    RectTransform stripRt;
    CanvasGroup stripGroup;
    Image stripFill;
    TMP_Text stripTitle, stripTime;
    string stripTitleShown = "";

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { Instance = this; UiModal.Register(this, () => IsOpen, Close); }

    void OnDestroy()
    {
        UiModal.Unregister(this);
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
        UiInputGuard.EnsureEventSystem();
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
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);   // включение запускает CandyPopIn окна
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
}
