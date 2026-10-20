// Assets/_Project/Scripts/UI/PetCrateWindow.cs (НЕ в Editor)
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

public class PetCrateWindow : MonoBehaviour
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
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Build();
    }

    static bool OtherWindowOpen()
    {
        var u = UpgradeWindow.Instance;
        if (u != null && u.IsOpen) return true;
        var p = PlankWorkshopWindow.Instance;
        if (p != null && p.IsOpen) return true;
        var sw = SellWindow.Instance;
        if (sw != null && sw.IsOpen) return true;
        var ww = WorkersWindow.Instance;
        if (ww != null && ww.IsOpen) return true;
        var bw = BuildWindow.Instance;
        if (bw != null && bw.IsOpen) return true;
        if (PetCollectionWindow.IsOpen || AchievementsWindow.IsOpen) return true;
        return HudMinimap.BigMapOpen || EndingSequence.Active;
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

    // ============================================================ частицы
    void Emit(float x, float y, float vx, float vy, float g, float life, float s0, float s1, Color c, float vrot)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (s.alive) continue;
            s.alive = true;
            s.x = x; s.y = y; s.vx = vx; s.vy = vy; s.g = g;
            s.life = s.max = life;
            s.s0 = s0; s.s1 = s1; s.col = c;
            s.rot = Random.Range(0f, 90f); s.vrot = vrot;
            s.img.gameObject.SetActive(true);
            s.rt.anchoredPosition = new Vector2(x, -y);
            return;
        }
    }

    // взрыв искр из точки (координаты внутри окна от левого верхнего угла, вниз положительно)
    void Burst(float cx, float cy, int n, Color accent, float vMin, float vMax)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float v = Random.Range(vMin, vMax);
            Color c = (i % 3 == 0) ? Color.white : (i % 3 == 1 ? accent : Color.Lerp(accent, UiKit.BrassHi, 0.5f));
            Emit(cx, cy, Mathf.Cos(a) * v, Mathf.Sin(a) * v - 120f, 700f, Random.Range(0.6f, 1.2f),
                 Random.Range(14f, 30f), 2f, c, Random.Range(-240f, 240f));
        }
    }

    void UpdateSparks(float dt)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (!s.alive) continue;
            s.life -= dt;
            if (s.life <= 0f)
            {
                s.alive = false;
                s.img.gameObject.SetActive(false);
                continue;
            }
            s.x += s.vx * dt;
            s.y += s.vy * dt;
            s.vy += s.g * dt;
            s.vx *= 1f - Mathf.Min(1f, 0.8f * dt);
            s.rot += s.vrot * dt;

            float t = 1f - s.life / s.max;
            float size = Mathf.Lerp(s.s0, s.s1, t);
            float a = Mathf.Clamp01(t * 12f) * Mathf.Pow(1f - t, 1.4f);

            s.rt.anchoredPosition = new Vector2(s.x, -s.y);
            s.rt.sizeDelta = new Vector2(size, size);
            s.rt.localEulerAngles = new Vector3(0f, 0f, s.rot);
            Color c = s.col; c.a = a * s.col.a;
            s.img.color = c;
        }
    }

    void ClearSparks()
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null) continue;
            sparks[i].alive = false;
            if (sparks[i].img != null) sparks[i].img.gameObject.SetActive(false);
        }
    }

    // ============================================================ экран 2: рулетка
    void BuildStrip(PetData winner, PetDatabase db)
    {
        // в ленте только те редкости, что реально выпадают из этой клети
        float[] odds = GameBalance.PetCrateOdds[(int)crate];
        var pool = new List<PetData>();
        if (db != null && db.pets != null)
            for (int i = 0; i < db.pets.Count; i++)
            {
                var pd = db.pets[i];
                if (pd == null) continue;
                int ri = (int)pd.rarity;
                if (ri < odds.Length && odds[ri] > 0f) pool.Add(pd);
            }
        if (pool.Count == 0) pool.Add(winner);

        PetData prev = null;
        for (int i = 0; i < CardCount; i++)
        {
            PetData d;
            if (i == WinIndex) d = winner;
            else
            {
                d = pool[Random.Range(0, pool.Count)];
                for (int t = 0; t < 4 && pool.Count > 1 && d == prev; t++) d = pool[Random.Range(0, pool.Count)];
            }
            prev = d;
            stripData[i] = d;
            stripCards[i].Set(d);
            stripCards[i].rt.localScale = new Vector3(0.84f, 0.84f, 1f);
        }

        stripX0 = MaskCenter - (StartIndex * Pitch + CardW * 0.5f);
        stripX1 = MaskCenter - (WinIndex * Pitch + CardW * 0.5f);       // здесь выпавшая карточка ровно под указателем
    }

    // карточки у центра крупнее, дальние мельче
    void UpdateStripLook(float x)
    {
        int c = Mathf.FloorToInt((MaskCenter - x) / Pitch);
        for (int i = c - 4; i <= c + 4; i++)
        {
            if (i < 0 || i >= CardCount) continue;
            float dist = Mathf.Abs(x + i * Pitch + CardW * 0.5f - MaskCenter);
            float s = Mathf.Lerp(1.05f, 0.84f, Mathf.Clamp01(dist / 380f));
            stripCards[i].rt.localScale = new Vector3(s, s, 1f);
        }
    }

    // имя и редкость карточки под указателем; ping: карточка редкая, указатель вспыхивает и сыплет искры
    void UpdateLive(int idx, bool ping)
    {
        idx = Mathf.Clamp(idx, 0, CardCount - 1);
        PetData d = stripData[idx];
        if (d == null) return;
        Color acc = RarityAccent(d.rarity);
        liveR = d.rarity;
        Set(liveName, PetUi.NameOf(d));
        liveName.color = acc;
        Set(liveRarity, PetText.Rarity(d.rarity).ToUpper());
        liveRarity.color = acc;
        popT = 1f;

        if (ping && d.rarity >= PetRarity.Rare && Time.unscaledTime - lastPingTime > 0.12f)
        {
            lastPingTime = Time.unscaledTime;
            pingT = 1f;
            Burst(InW * 0.5f, PointerCy, d.rarity == PetRarity.Legendary ? 9 : 5, acc, 120f, 320f);
        }
    }

    void SetPointerColor(Color c)
    {
        for (int i = 0; i < pointerBars.Count; i++) pointerBars[i].color = c;
    }

    void AnimateRoll(float dt)
    {
        rollT += dt;
        rollCg.alpha = Mathf.Clamp01(rollT / IntroTime);

        float u = Mathf.Clamp01(rollT / RollTime);
        float e = 1f - Mathf.Pow(1f - u, 3.5f);                          // ease-out: быстро в начале, плавно в конце
        float x = u >= 1f ? stripX1 : Mathf.Lerp(stripX0, stripX1, e);
        stripRt.anchoredPosition = new Vector2(x, 0f);
        UpdateStripLook(x);

        int idx = Mathf.FloorToInt((MaskCenter - x) / Pitch);            // карточка под указателем
        if (idx != lastTickIdx)
        {
            lastTickIdx = idx;
            UpdateLive(idx, true);
            if (u < 1f && Time.unscaledTime - lastTickTime > 0.035f)
            {
                lastTickTime = Time.unscaledTime;
                SfxManager.Play("ui", 0.3f, Mathf.Lerp(1.4f, 0.85f, e));
            }
        }

        // указатель подсвечивается цветом редкости карточки под ним, при редкой вспыхивает и «пульсирует»
        pointerK = Mathf.MoveTowards(pointerK, liveR >= PetRarity.Uncommon ? 1f : 0f, dt * 10f);
        pingT = Mathf.MoveTowards(pingT, 0f, dt * 3f);
        popT = Mathf.MoveTowards(popT, 0f, dt * 6f);
        Color pc = Color.Lerp(UiKit.Brass, RarityAccent(liveR), pointerK);
        SetPointerColor(Color.Lerp(pc, Color.white, pingT * 0.6f));
        float ps = 1f + 0.05f * pingT;
        pointerRt.localScale = new Vector3(ps, ps, 1f);
        float lp = 1f + 0.12f * popT;
        liveName.rectTransform.localScale = new Vector3(lp, lp, 1f);
        liveRarity.rectTransform.localScale = new Vector3(lp, lp, 1f);

        if (rollT >= RollTime + HoldTime) ShowResult();
    }

    // ============================================================ экран 1: подготовка (анимация)
    void AnimatePrepare(float dt)
    {
        float t = Time.unscaledTime;
        Color ca = PetUi.CrateAccent(crate);

        // ящик слегка покачивается, ореол дышит
        float sc = 1f + 0.035f * Mathf.Sin(t * 2.4f);
        crateImg.rectTransform.localScale = new Vector3(sc, sc, 1f);
        crateImg.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 1.3f) * 2.5f);
        Color h = ca; h.a = 0.30f + 0.10f * Mathf.Sin(t * 2f);
        crateHalo.color = h;
        Color rc = ca; rc.a = 0.16f + 0.04f * Mathf.Sin(t * 1.5f);
        crateRays.color = rc;
        crateRays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -t * 8f);

        // строки шансов выезжают по очереди, полосы набирают длину
        prepT += dt;
        for (int i = 0; i < 4; i++)
        {
            if (!oddsRow[i].activeSelf) continue;
            float k = Mathf.Clamp01((prepT - i * 0.08f) / 0.28f);
            float e = UiDraw.EaseOutCubic(k);
            oddsCg[i].alpha = k;
            oddsRt[i].anchoredPosition = new Vector2(470f + (1f - e) * 50f, -(76f + i * 62f));
            float k2 = UiDraw.EaseOutCubic(Mathf.Clamp01((prepT - 0.15f - i * 0.08f) / 0.6f));
            oddsBar[i].rectTransform.anchorMax = new Vector2(oddsTarget[i] * k2, 1f);
        }

        // искры поднимаются вокруг ящика
        ambientT -= dt;
        if (ambientT <= 0f)
        {
            ambientT = 0.16f;
            Emit(CrateCx + Random.Range(-100f, 100f), CrateCy + Random.Range(-10f, 100f),
                 Random.Range(-14f, 14f), -Random.Range(30f, 70f), 0f, Random.Range(1.1f, 1.7f),
                 Random.Range(9f, 18f), 0f, Random.value < 0.4f ? Color.white : ca, Random.Range(-60f, 60f));
        }
    }

    // ============================================================ экран 3: выпадение
    void ShowResult()
    {
        if (result == null || result.data == null) { SetStep(Step.Prepare); Refresh(); return; }
        var pm = PetManager.Instance;
        PetData d = result.data;
        resultData = d;
        Color accent = RarityAccent(d.rarity);

        SetStep(Step.Result);
        resultCard.LiveStage = PetManager.Instance != null ? PetManager.Instance.GrowthStage : 2;
        resultCard.Set(d);

        Set(nameText, PetUi.NameOf(d));
        nameText.color = Color.white;
        Set(rarityText, PetText.Rarity(d.rarity));
        rarityText.color = accent;
        gemL.color = accent; gemR.color = accent;

        // строка стата с иконкой по центру
        Set(statText, PetUi.StatLine(d));
        statBadgeIcon.sprite = UiKit.Icon(PetUi.StatIcon(d.stat));
        float tw = statText.GetPreferredValues(statText.text).x;
        float total = 46f + 12f + tw;
        float sx = (InW - total) * 0.5f;
        TLC(statBadge.rectTransform, sx + 23f, 401f, 46f, 46f);
        TL(statText.rectTransform, sx + 58f, 382f, tw + 24f, 38f);

        int count = pm != null ? pm.CountOf(d) : 1;
        isNew = count <= 1;
        Set(dupText, isNew
            ? "<color=#" + Hex(UiKit.Good) + ">Новый вид в коллекции!</color>"
            : "Дубликат: теперь у тебя x" + count
                + (count >= GameBalance.PetMergeCount ? "  <size=70%><color=#" + Hex(Dim) + ">(можно слить в окне питомцев, клавиша P)</color></size>" : ""));

        resultT = 0f;
        flashT = 0f;
        flashed = false;
        backGo.SetActive(true);
        resultCard.rt.localScale = Vector3.one;
        resultInfo.alpha = 0f;
        resultInfo.blocksRaycasts = false;
        newTag.gameObject.SetActive(false);

        Color c = accent; c.a = 0f;
        burst.color = c; glowIdle.color = c; rays.color = c;
        cardFlash.color = new Color(1f, 1f, 1f, 0f);
        screenFlash.color = c;
        burst.rectTransform.localScale = Vector3.one * 0.4f;
        cardFlash.rectTransform.SetAsLastSibling();

        RefreshResultButtons();
    }

    // сила эффектов по редкости: 0 обычный .. 3 легендарный
    static float RayAlpha(PetRarity r)
    {
        switch (r)
        {
            case PetRarity.Common: return 0f;
            case PetRarity.Uncommon: return 0.16f;
            case PetRarity.Rare: return 0.34f;
            default: return 0.55f;
        }
    }

    void OnReveal(PetRarity rar, Color accent)
    {
        int n = rar == PetRarity.Legendary ? 46 : rar == PetRarity.Rare ? 30 : rar == PetRarity.Uncommon ? 20 : 14;
        Burst(CardCx, CardCy, n, accent, 220f, rar >= PetRarity.Rare ? 720f : 520f);
        if (rar == PetRarity.Legendary) { shakeAmp = 16f; shakeT = 0.45f; }
        else if (rar == PetRarity.Rare) { shakeAmp = 9f; shakeT = 0.45f; }
    }

    void AnimateResult(float dt)
    {
        resultT += dt;
        float f = Mathf.Clamp01(resultT / FlipTime);

        float sx;
        if (f < 0.5f) sx = Mathf.Lerp(1f, 0f, f * 2f);
        else sx = Mathf.Lerp(0f, 1f, UiDraw.EaseOutCubic((f - 0.5f) * 2f));
        float sy = 1f + 0.06f * Mathf.Sin(f * Mathf.PI);
        resultCard.rt.localScale = new Vector3(Mathf.Max(sx, 0.02f), sy, 1f);
        backGo.SetActive(f < 0.5f);

        PetRarity rar = resultData != null ? resultData.rarity : PetRarity.Common;
        Color accent = RarityAccent(rar);

        if (!flashed && f >= 0.5f)
        {
            flashed = true;
            flashT = 0f;
            PlayRevealSfx();
            OnReveal(rar, accent);
        }

        if (flashed)
        {
            flashT += dt;
            float fu = Mathf.Clamp01(flashT / FlashTime);

            Color b = accent; b.a = (1f - fu) * 0.9f;
            burst.color = b;
            burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.7f, UiDraw.EaseOutCubic(fu));

            Color cf = Color.Lerp(accent, Color.white, 0.55f); cf.a = (1f - fu) * 0.75f;
            cardFlash.color = cf;

            float baseA = rar >= PetRarity.Rare ? 0.45f : 0.22f;
            Color g = accent;
            g.a = baseA * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.5f)) * Mathf.Clamp01(flashT / 0.3f);
            glowIdle.color = g;

            // вращающиеся лучи за карточкой
            Color rc = accent; rc.a = RayAlpha(rar) * Mathf.Clamp01(flashT / 0.5f);
            rays.color = rc;
            rays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * (10f + 6f * (int)rar));
            float rs = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 1.7f);
            rays.rectTransform.localScale = new Vector3(rs, rs, 1f);

            // цветная вспышка на весь экран для редких
            float fa = rar == PetRarity.Legendary ? 0.30f : rar == PetRarity.Rare ? 0.20f : rar == PetRarity.Uncommon ? 0.10f : 0f;
            Color sf = accent; sf.a = fa * (1f - fu);
            screenFlash.color = sf;

            // тег «НОВЫЙ!» выскакивает с отскоком
            if (isNew)
            {
                float tt = Mathf.Clamp01((flashT - 0.25f) / 0.35f);
                newTag.gameObject.SetActive(tt > 0f);
                float ts = Mathf.Max(0.01f, UiDraw.EaseOutBack(tt));
                newTag.rectTransform.localScale = new Vector3(ts, ts, 1f);
            }

            // у Необычных и выше вокруг карточки не гаснут искры
            if (rar >= PetRarity.Uncommon)
            {
                ambientT -= dt;
                if (ambientT <= 0f)
                {
                    ambientT = rar == PetRarity.Legendary ? 0.05f : rar == PetRarity.Rare ? 0.09f : 0.18f;
                    Emit(CardCx + Random.Range(-170f, 170f), CardCy + Random.Range(-120f, 140f),
                         Random.Range(-16f, 16f), -Random.Range(40f, 90f), 0f, Random.Range(1f, 1.6f),
                         Random.Range(10f, 20f), 0f, Random.value < 0.45f ? Color.white : accent, Random.Range(-90f, 90f));
                }
            }

            // камни редкости и медальон стата выскакивают с отскоком
            float pk = UiDraw.EaseOutBack(Mathf.Clamp01((flashT - 0.12f) / 0.4f));
            gemL.rectTransform.localScale = Vector3.one * pk;
            gemR.rectTransform.localScale = Vector3.one * pk;
            statBadge.rectTransform.localScale = Vector3.one * UiDraw.EaseOutBack(Mathf.Clamp01((flashT - 0.25f) / 0.4f));

            resultInfo.alpha = Mathf.Clamp01(flashT / 0.35f);
            resultInfo.blocksRaycasts = resultInfo.alpha > 0.6f;
        }
    }

    void Animate(float dt)
    {
        UpdateSparks(dt);
        if (step == Step.Prepare) AnimatePrepare(dt);
        else if (step == Step.Roll) AnimateRoll(dt);
        else if (step == Step.Result) AnimateResult(dt);
    }

    void PlayRevealSfx()
    {
        PetRarity r = resultData != null ? resultData.rarity : PetRarity.Common;
        switch (r)
        {
            case PetRarity.Common: SfxManager.Play("coin", 0.8f, 1f); break;
            case PetRarity.Uncommon: SfxManager.Play("coin", 0.9f, 1.2f); break;
            case PetRarity.Rare:
                SfxManager.Play("level", 0.9f, 1f);
                SfxManager.Play("coin", 0.6f, 1.4f);
                break;
            default:
                SfxManager.Play("quest", 1f, 1f);
                SfxManager.Play("level", 0.7f, 1.25f);
                break;
        }
    }

    void RefreshResultButtons()
    {
        var pm = PetManager.Instance;
        if (pm == null || result == null) return;

        bool isEq = pm.IsEquipped(result.uid);
        bool free = pm.EquippedCount < pm.SlotCount;
        SetBtn(equipBtn, equipLabel, !isEq && free, UiKit.Navy);
        Set(equipLabel, isEq ? "Взят" : "Взять с собой");

        if (isEq) Set(slotNote, "<color=#" + Hex(UiKit.Good) + ">Питомец теперь с тобой</color>");
        else if (!free) Set(slotNote, "<color=#" + Hex(UiKit.Warn) + ">Все слоты заняты</color>");
        else Set(slotNote, "");
    }

    // ============================================================ обновление
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Button b, TMP_Text label, bool on, Color onColor)
    {
        if (b == null) return;
        b.interactable = on;
        if (label != null) label.color = on ? onColor : OffText;
    }

    void Refresh()
    {
        refreshAt = Time.unscaledTime + 0.1f;
        if (!built) return;
        if (inv == null) inv = PlayerInventory.Instance;

        Set(coinsText, (inv != null ? inv.coinsCount : 0).ToString());
        Set(titleText, PetText.Crate(crate).ToUpper());

        if (step == Step.Prepare) RefreshPrepare();
        else if (step == Step.Result) RefreshResultButtons();
    }

    void SetPriceLayout(bool free)
    {
        int want = free ? 1 : 0;
        if (priceLayout == want) return;
        priceLayout = want;
        priceCoin.gameObject.SetActive(!free);
        if (free)
        {
            TL(priceText.rectTransform, 0f, 378f, 420f, 50f);
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.color = UiKit.Good;
        }
        else
        {
            TL(priceText.rectTransform, 182f, 378f, 220f, 50f);
            priceText.alignment = TextAlignmentOptions.Left;
            priceText.color = UiKit.BrassHi;
        }
    }

    void RefreshPrepare()
    {
        var pm = PetManager.Instance;
        int price = pm != null ? pm.Price(crate) : GameBalance.PetCratePrice[(int)crate];
        SetPriceLayout(price <= 0);
        Set(priceText, price <= 0 ? "БЕСПЛАТНО" : price.ToString());

        // шансы: показываем только редкости с шансом больше нуля, рядом сколько видов в этой редкости
        float[] odds = GameBalance.PetCrateOdds[(int)crate];
        int row = 0;
        for (int r = 0; r < 4; r++)
        {
            float v = r < odds.Length ? odds[r] : 0f;
            if (v <= 0f) continue;
            Color acc = RarityAccent((PetRarity)r);
            oddsRow[row].SetActive(true);
            oddsGem[row].color = acc;
            int kinds = 0;
            if (pm != null && pm.database != null)
            {
                pm.database.CollectByRarity((PetRarity)r, tmpPets);
                kinds = tmpPets.Count;
            }
            Set(oddsName[row], PetText.Rarity((PetRarity)r)
                + "  <size=60%><color=#" + Hex(Dim) + ">" + kinds + " " + UiDraw.Plural(kinds, "вид", "вида", "видов") + "</color></size>");
            oddsName[row].color = acc;
            Set(oddsPct[row], v.ToString("0.#", CultureInfo.InvariantCulture) + "%");
            oddsBar[row].color = RarityFill((PetRarity)r);
            oddsTarget[row] = Mathf.Clamp(v / 100f, 0.07f, 1f);
            row++;
        }
        for (; row < 4; row++) oddsRow[row].SetActive(false);

        // гарантия: полоса заполняется с каждой клетью без Редкого
        if (pm != null)
        {
            int until = pm.OpensUntilGuarantee(crate);
            bool next = until <= 1;
            Set(guaranteeText, next
                ? "<color=#" + Hex(UiKit.Warn) + ">Следующая клеть даст Редкого!</color>"
                : "До Редкого: <b>" + until + "</b>  <size=70%><color=#" + Hex(Dim) + ">(гарантия каждые " + GameBalance.PetGuaranteeEvery + ")</color></size>");
            pityFill.fillAmount = Mathf.Clamp01((GameBalance.PetGuaranteeEvery - until) / (float)Mathf.Max(1, GameBalance.PetGuaranteeEvery - 1));
        }
        else
        {
            Set(guaranteeText, "");
            pityFill.fillAmount = 0f;
        }

        // можно ли открыть, и если нет, то почему
        bool ok = false;
        string reason = "";
        if (pm == null)
            reason = "Питомцы недоступны: на сцене нет PetManager";
        else
        {
            PetOpenResult why;
            ok = pm.CanOpen(crate, out why);
            if (!ok)
            {
                switch (why)
                {
                    case PetOpenResult.NoCoins: reason = "Не хватает монет"; break;
                    case PetOpenResult.Locked: reason = "С " + GameBalance.PetCrateLevel[(int)crate] + " уровня"; break;
                    case PetOpenResult.NoDatabase: reason = "Питомцы недоступны: не подключена база питомцев"; break;
                    default: reason = "Питомцы недоступны"; break;
                }
            }
        }
        Set(reasonText, reason);
        SetBtn(openBtn, openLabel, ok, UiKit.Navy);
    }

    // ============================================================ построение окна
    void Build()
    {
        if (built) return;

        var cgo = new GameObject("PetCrateCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31;                                   // выше окна прокачки (30)
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        // затемнение: только гасит клики по миру
        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        // окно: деревянная планка сверху
        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        win.raycastTarget = true;                                   // клик по окну не закрывает его
        panel = win.rectTransform;
        Anch(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);

        var inner = UiKit.MakePanel(content, new Vector2(InW, InH), false, 0.6f, "Inner");
        Stretch(inner.rectTransform);

        prepareGo = NewScreen("Prepare", inner.transform);
        rollGo = NewScreen("Roll", inner.transform);
        resultGo = NewScreen("Result", inner.transform);
        contentsGo = NewScreen("Contents", inner.transform);
        oddsGo = NewScreen("Odds", inner.transform);
        BuildPrepare(prepareGo.transform);
        BuildRoll(rollGo.transform);
        BuildResult(resultGo.transform);
        BuildContents(contentsGo.transform);
        BuildOdds(oddsGo.transform);
        BuildFx(inner.transform);

        // цветная вспышка поверх окна (для редких дропов), клики пропускает
        screenFlash = UiKit.MakeImage(ov.transform, "ScreenFlash", null, false);
        Stretch(screenFlash.rectTransform);
        screenFlash.color = Clear;

        hintText = UiKit.MakeLabel(ov.transform, "", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(1000, 34));

        ov.gameObject.SetActive(false);
        built = true;
        step = Step.Prepare;
        SetStep(Step.Prepare);
    }

    static GameObject NewScreen(string name, Transform parent)
    {
        var rt = NewRT(name, parent);
        Stretch(rt);
        return rt.gameObject;
    }

    // слой искр поверх всех экранов, координаты как у TL: от левого верхнего угла окна вниз
    void BuildFx(Transform inner)
    {
        fx = NewRT("Fx", inner);
        Stretch(fx);
        for (int i = 0; i < sparks.Length; i++)
        {
            var im = UiKit.MakeImage(fx, "Spark", PetUi.SparkSprite, false);
            Anch(im.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            im.gameObject.SetActive(false);
            sparks[i] = new Spark { img = im, rt = im.rectTransform };
        }
    }

    void BuildHeader()
    {
        // монеты слева на деревянной планке
        var coin = UiKit.MakeIcon(panel, UiIcon.Coin, 36f, "CoinIcon");
        TLC(coin.rectTransform, 64, 27, 36, 36);
        coinsText = UiKit.MakeLabel(panel, "0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(coinsText.rectTransform, 90, 8, 200, 38);

        // заголовок по центру
        titleText = UiKit.MakeLabel(panel, "КЛЕТЬ", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(titleText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(520, 40));

        // закрыть (на рулетке пропускает её)
        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.onClick.AddListener(OnCloseButton);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    // ------------------------------------------------------------ экран 1
    void BuildPrepare(Transform p)
    {
        // слева: лучи, ореол, ящик, цена
        crateRays = UiKit.MakeImage(p, "CrateRays", PetUi.RaysSprite, false);
        TLC(crateRays.rectTransform, CrateCx, CrateCy, 440f, 440f);
        crateRays.color = Clear;
        crateHalo = UiKit.MakeImage(p, "Halo", PetUi.GlowSprite, false);
        TLC(crateHalo.rectTransform, CrateCx, CrateCy, 460f, 460f);
        crateHalo.color = Clear;

        crateImg = UiKit.MakeIcon(p, UiIcon.Box, 210f, "CrateIcon");
        TLC(crateImg.rectTransform, CrateCx, CrateCy, 210f, 210f);

        priceCoin = UiKit.MakeIcon(p, UiIcon.Coin, 46f, "PriceCoin");
        TLC(priceCoin.rectTransform, 150f, 402f, 46f, 46f);
        priceText = UiKit.MakeLabel(p, "0", 46f, UiKit.BrassHi, TextAlignmentOptions.Left, true);
        TL(priceText.rectTransform, 182f, 378f, 220f, 50f);

        // справа: шансы выпадения (камень, имя, полоса, процент)
        var head = UiKit.MakeLabel(p, "Шансы выпадения", 26f, Soft, TextAlignmentOptions.Left, true);
        TL(head.rectTransform, 470f, 30f, 500f, 34f);
        for (int i = 0; i < 4; i++)
        {
            var rt = NewRT("Odds" + i, p);
            TL(rt, 470f, 76f + i * 62f, 500f, 54f);
            oddsRow[i] = rt.gameObject;
            oddsRt[i] = rt;
            oddsCg[i] = rt.gameObject.AddComponent<CanvasGroup>();

            var bg = UiKit.MakeImage(rt, "Bg", UiKit.Slot, true, 0.45f);
            Stretch(bg.rectTransform);

            oddsGem[i] = UiKit.MakeIcon(rt, UiIcon.Gem, 38f, "Gem");
            TLC(oddsGem[i].rectTransform, 34f, 27f, 38f, 38f);

            oddsName[i] = UiKit.MakeLabel(rt, "", 28f, Color.white, TextAlignmentOptions.Left, true);
            TL(oddsName[i].rectTransform, 64f, 0f, 204f, 54f);
            PetUi.AutoSize(oddsName[i], 16f, 28f);

            // полоса шанса: тёмная рамка и заливка цветом редкости, ширина = процент
            var barBg = UiKit.MakeImage(rt, "BarBg", UiKit.BarFrame, true, 0.5f);
            TL(barBg.rectTransform, 272f, 17f, 130f, 20f);
            oddsBar[i] = UiKit.MakeImage(barBg.rectTransform, "BarFill", null, false);
            var fr = oddsBar[i].rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 1f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = new Vector2(4f, 4f);
            fr.offsetMax = new Vector2(-4f, -4f);

            oddsPct[i] = UiKit.MakeLabel(rt, "", 28f, Color.white, TextAlignmentOptions.Right, true);
            TL(oddsPct[i].rectTransform, 406f, 0f, 84f, 54f);
        }

        // гарантия: подпись и синяя полоса
        guaranteeText = UiKit.MakeLabel(p, "", 26f, Soft, TextAlignmentOptions.Left, false);
        TL(guaranteeText.rectTransform, 470f, 338f, 500f, 34f);
        Image pf;
        var pityBar = UiKit.MakeBar(p, UiBar.Blue, new Vector2(400f, 30f), out pf, 0.6f);
        TL(pityBar.rectTransform, 470f, 378f, 400f, 30f);
        pityFill = pf;

        // причина блокировки жёлтым
        reasonText = UiKit.MakeLabel(p, "", 26f, UiKit.Warn, TextAlignmentOptions.Left, true);
        TL(reasonText.rectTransform, 470f, 420f, 500f, 70f);

        // что может выпасть
        TMP_Text tmpL;
        MakeBtn(p, "Что внутри?", UiBtn.Green, 14f, 440f, 205f, 60f, 24f, OpenContents, out tmpL);
        MakeBtn(p, "Шансы", UiBtn.Amber, 229f, 440f, 205f, 60f, 24f, OpenOdds, out tmpL);

        openBtn = MakeBtn(p, "Открыть", UiBtn.Amber, (InW - 380f) * 0.5f, 508f, 380f, 92f, 36f, StartRoll, out openLabel);
    }

    // ------------------------------------------------------------ экран 2
    void BuildRoll(Transform p)
    {
        rollCg = p.gameObject.AddComponent<CanvasGroup>();

        rollTitle = UiKit.MakeLabel(p, "", 38f, Color.white, TextAlignmentOptions.Center, true);
        TL(rollTitle.rectTransform, 0f, 74f, InW, 50f);

        var view = UiKit.MakeImage(p, "Viewport", UiKit.Slot, true, 1f);
        TL(view.rectTransform, (InW - ViewW) * 0.5f, 150f, ViewW, ViewH);

        var mask = NewRT("Mask", view.transform);
        Stretch(mask);
        mask.offsetMin = new Vector2(MaskInset, MaskInset);
        mask.offsetMax = new Vector2(-MaskInset, -MaskInset);
        mask.gameObject.AddComponent<RectMask2D>();

        stripRt = NewRT("Strip", mask);
        Anch(stripRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(CardCount * Pitch, CardH));

        // карточки создаются один раз, при каждом броске только меняются данные
        var mid = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < CardCount; i++)
        {
            var cv = PetCardView.Create(stripRt, CardW, CardH, "Card" + i, true);
            Anch(cv.rt, new Vector2(0, 0.5f), mid, new Vector2(i * Pitch + CardW * 0.5f, 0f), new Vector2(CardW, CardH));
            cv.rt.localScale = new Vector3(0.84f, 0.84f, 1f);
            stripCards[i] = cv;
        }

        // края ленты гаснут в цвет окна
        var fadeCol = SlotDark;
        var fl = UiKit.MakeImage(view.transform, "FadeL", PetUi.EdgeFadeSprite, false);
        fl.color = fadeCol;
        Anch(fl.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(MaskInset, 0f), new Vector2(150f, ViewH - 2f * MaskInset));
        var fr = UiKit.MakeImage(view.transform, "FadeR", PetUi.EdgeFadeSprite, false);
        fr.color = fadeCol;
        Anch(fr.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-MaskInset, 0f), new Vector2(150f, ViewH - 2f * MaskInset));
        fr.rectTransform.localScale = new Vector3(-1f, 1f, 1f);          // зеркало вокруг левой кромки: уходит внутрь окна

        // неподвижная рамка-указатель по центру (в группе, чтобы «пульсировать» целиком)
        pointerRt = NewRT("PointerGroup", view.transform);
        Anch(pointerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        float fw = CardW + 14f, fh = CardH + 14f, t = 5f;
        PointerBar(pointerRt, new Vector2(0, fh * 0.5f - t * 0.5f), new Vector2(fw, t));
        PointerBar(pointerRt, new Vector2(0, -fh * 0.5f + t * 0.5f), new Vector2(fw, t));
        PointerBar(pointerRt, new Vector2(-fw * 0.5f + t * 0.5f, 0), new Vector2(t, fh));
        PointerBar(pointerRt, new Vector2(fw * 0.5f - t * 0.5f, 0), new Vector2(t, fh));

        var tailTop = UiKit.MakeImage(view.transform, "TailTop", UiKit.Tail, false);
        Anch(tailTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(43, 26));
        var tailBot = UiKit.MakeImage(view.transform, "TailBottom", UiKit.Tail, false);
        Anch(tailBot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(43, 26));
        tailBot.rectTransform.localEulerAngles = new Vector3(0, 0, 180f);

        // имя и редкость карточки под указателем, меняются на ходу
        liveName = UiKit.MakeLabel(p, "", 42f, Color.white, TextAlignmentOptions.Center, true);
        TLC(liveName.rectTransform, InW * 0.5f, 443f, InW, 50f);
        PetUi.AutoSize(liveName, 24f, 42f);
        liveRarity = UiKit.MakeLabel(p, "", 26f, Color.white, TextAlignmentOptions.Center, true);
        TLC(liveRarity.rectTransform, InW * 0.5f, 487f, InW, 34f);
    }

    void PointerBar(Transform parent, Vector2 pos, Vector2 size)
    {
        var img = UiKit.MakeImage(parent, "Pointer", null, false);
        img.color = UiKit.Brass;
        Anch(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        pointerBars.Add(img);
    }

    // ------------------------------------------------------------ экран 3
    void BuildResult(Transform p)
    {
        // лучи, свечение и вспышка за карточкой. Лежат в маске, чтобы не вылезать за рамку окна
        const float inset = 10f;
        var clip = NewRT("RaysClip", p);
        Stretch(clip);
        clip.offsetMin = new Vector2(inset, inset);
        clip.offsetMax = new Vector2(-inset, -inset);
        clip.gameObject.AddComponent<RectMask2D>();

        rays = UiKit.MakeImage(clip, "Rays", PetUi.RaysSprite, false);
        TLC(rays.rectTransform, CardCx - inset, CardCy - inset, 780f, 780f);
        rays.color = Clear;
        glowIdle = UiKit.MakeImage(clip, "GlowIdle", PetUi.GlowSprite, false);
        TLC(glowIdle.rectTransform, CardCx - inset, CardCy - inset, 560f, 560f);
        glowIdle.color = Clear;
        burst = UiKit.MakeImage(clip, "Burst", PetUi.GlowSprite, false);
        TLC(burst.rectTransform, CardCx - inset, CardCy - inset, 560f, 560f);
        burst.color = Clear;

        resultCard = PetCardView.Create(p, 220f, 270f, "ResultCard", true);
        resultCard.EnableLive();     // выпавший питомец вращается живой 3D-моделью
        TLC(resultCard.rt, CardCx, CardCy, 220f, 270f);

        // рубашка (видна до переворота) и вспышка поверх карточки
        var back = UiKit.MakeImage(resultCard.rt, "Back", UiKit.Slot, true, 1f);
        Stretch(back.rectTransform);
        backGo = back.gameObject;
        var boxIcon = UiKit.MakeIcon(back.transform, UiIcon.Box, 120f, "BackIcon");
        boxIcon.rectTransform.anchoredPosition = Vector2.zero;

        cardFlash = UiKit.MakeImage(resultCard.rt, "CardFlash", null, false);
        Stretch(cardFlash.rectTransform);
        cardFlash.rectTransform.offsetMin = new Vector2(7f, 7f);
        cardFlash.rectTransform.offsetMax = new Vector2(-7f, -7f);
        cardFlash.color = Clear;

        // тег «НОВЫЙ!» на уголке карточки (деревянная табличка, чуть наклонена)
        newTag = UiKit.MakePlaque(p, new Vector2(140f, 58f), 0.9f, "NewTag");
        TLC(newTag.rectTransform, CardCx + 135f, CardCy - 105f, 140f, 58f);
        newTag.rectTransform.localEulerAngles = new Vector3(0f, 0f, -9f);
        newTagLabel = UiKit.MakeLabel(newTag.transform, "НОВЫЙ!", 30f, WoodText, TextAlignmentOptions.Center, true);
        PetUi.AutoSize(newTagLabel, 16f, 30f);
        newTag.gameObject.SetActive(false);

        // текст и кнопки появляются после переворота
        var info = NewRT("Info", p);
        Stretch(info);
        resultInfo = info.gameObject.AddComponent<CanvasGroup>();
        resultInfo.alpha = 0f;

        nameText = UiKit.MakeLabel(info, "", 46f, Color.white, TextAlignmentOptions.Center, true);
        TL(nameText.rectTransform, 0f, 296f, InW, 52f);

        // редкость между двумя камнями
        rarityText = UiKit.MakeLabel(info, "", 28f, Color.white, TextAlignmentOptions.Center, true);
        TL(rarityText.rectTransform, 0f, 346f, InW, 34f);
        gemL = UiKit.MakeIcon(info, UiIcon.Gem, 32f, "GemL");
        TLC(gemL.rectTransform, CardCx - 140f, 363f, 32f, 32f);
        gemR = UiKit.MakeIcon(info, UiIcon.Gem, 32f, "GemR");
        TLC(gemR.rectTransform, CardCx + 140f, 363f, 32f, 32f);

        // строка стата: медальон с иконкой и текст (позиции задаёт ShowResult по ширине текста)
        statBadge = UiKit.MakeImage(info, "StatBadge", UiKit.Medallion, false);
        TLC(statBadge.rectTransform, CardCx - 100f, 401f, 46f, 46f);
        statBadgeIcon = UiKit.MakeImage(statBadge.rectTransform, "StatIcon", null, false);
        statBadgeIcon.preserveAspect = true;
        Anch(statBadgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(29f, 29f));
        statText = UiKit.MakeLabel(info, "", 30f, Color.white, TextAlignmentOptions.Left, false);
        TL(statText.rectTransform, CardCx - 40f, 382f, 400f, 38f);

        dupText = UiKit.MakeLabel(info, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(dupText.rectTransform, 0f, 424f, InW, 30f);
        slotNote = UiKit.MakeLabel(info, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(slotNote.rectTransform, 0f, 456f, InW, 34f);

        float bx = (InW - (ResBtnW * 3f + ResBtnGap * 2f)) * 0.5f;
        TMP_Text tmp;
        equipBtn = MakeBtn(info, "Взять с собой", UiBtn.Amber, bx, ResBtnY, ResBtnW, ResBtnH, 30f, OnEquip, out equipLabel);
        MakeBtn(info, "В коллекцию", UiBtn.Green, bx + ResBtnW + ResBtnGap, ResBtnY, ResBtnW, ResBtnH, 30f, DoClose, out tmp);
        MakeBtn(info, "Открыть ещё", UiBtn.Yellow, bx + (ResBtnW + ResBtnGap) * 2f, ResBtnY, ResBtnW, ResBtnH, 30f, OnAgain, out tmp);
    }

    // ------------------------------------------------------------ экран 4: что может выпасть
    const int ContCols = 7;
    const float ContCardW = 120f, ContCardH = 142f, ContPitchX = 136f, ContRowH = 192f, ContHeadH = 46f;

    void OpenContents()
    {
        if (step != Step.Prepare) return;
        PopulateContents();
        SetStep(Step.Contents);
        SfxManager.Play("hint", 0.5f, 1.2f);
    }

    void CloseContents()
    {
        if (step != Step.Contents) return;
        SetStep(Step.Prepare);
        Refresh();
    }

    void BuildContents(Transform p)
    {
        var head = UiKit.MakeLabel(p, "Что может выпасть", 30f, Color.white, TextAlignmentOptions.Center, true);
        TL(head.rectTransform, 0f, 22f, InW, 40f);

        float vw = InW - 40f;
        var view = UiKit.MakeImage(p, "View", UiKit.Slot, true, 1f);
        TL(view.rectTransform, 20f, 70f, vw, 372f);

        // окно прокрутки: маска, невидимая картинка ловит перетаскивание, внутри длинный контент
        var vp = NewRT("Viewport", view.transform);
        Stretch(vp);
        vp.offsetMin = new Vector2(8f, 8f);
        vp.offsetMax = new Vector2(-8f, -8f);
        vp.gameObject.AddComponent<RectMask2D>();
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = Clear;
        vpImg.raycastTarget = true;

        contentsRt = NewRT("Content", vp);
        contentsRt.anchorMin = new Vector2(0f, 1f);
        contentsRt.anchorMax = new Vector2(1f, 1f);
        contentsRt.pivot = new Vector2(0.5f, 1f);
        contentsRt.anchoredPosition = Vector2.zero;
        contentsRt.sizeDelta = new Vector2(0f, 100f);

        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp;
        sr.content = contentsRt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        sr.inertia = true;

        // полоса с описанием выбранного питомца
        var bar = UiKit.MakeImage(p, "Detail", UiKit.Slot, true, 0.6f);
        TL(bar.rectTransform, 20f, 450f, vw, 62f);
        var badge = UiKit.MakeImage(bar.transform, "Badge", UiKit.Medallion, false);
        TLC(badge.rectTransform, 40f, 31f, 46f, 46f);
        detailBadgeIcon = UiKit.MakeImage(badge.transform, "StatIcon", null, false);
        detailBadgeIcon.preserveAspect = true;
        Anch(detailBadgeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(29f, 29f));
        detailGem = UiKit.MakeIcon(bar.transform, UiIcon.Gem, 32f, "Gem");
        TLC(detailGem.rectTransform, 90f, 31f, 32f, 32f);
        detailName = UiKit.MakeLabel(bar.transform, "", 28f, Color.white, TextAlignmentOptions.Left, true);
        TL(detailName.rectTransform, 114f, 0f, 330f, 62f);
        PetUi.AutoSize(detailName, 16f, 28f);
        detailStat = UiKit.MakeLabel(bar.transform, "", 26f, Color.white, TextAlignmentOptions.Left, false);
        TL(detailStat.rectTransform, 450f, 0f, vw - 470f, 62f);
        PetUi.AutoSize(detailStat, 16f, 26f);

        TMP_Text tmpL;
        MakeBtn(p, "Назад", UiBtn.Green, (InW - 300f) * 0.5f, 530f, 300f, 76f, 32f, CloseContents, out tmpL);
    }

    // заполняется при каждом открытии: питомцы по группам редкости (редкие сверху), шанс на каждый вид
    void PopulateContents()
    {
        for (int i = contentsRt.childCount - 1; i >= 0; i--)
        {
            var ch = contentsRt.GetChild(i).gameObject;
            ch.SetActive(false);
            Destroy(ch);
        }
        contentCards.Clear();
        selectedCard = null;
        SelectPet(null, null);

        var pm = PetManager.Instance;
        float[] odds = GameBalance.PetCrateOdds[(int)crate];
        float inW = InW - 40f - 16f;
        float left = (inW - (ContCols * ContPitchX - 16f)) * 0.5f;
        float y = 8f;
        bool any = false;

        if (pm != null && pm.database != null)
        {
            for (int r = 3; r >= 0; r--)
            {
                float v = r < odds.Length ? odds[r] : 0f;
                if (v <= 0f) continue;
                pm.database.CollectByRarity((PetRarity)r, tmpPets);
                if (tmpPets.Count == 0) continue;
                any = true;
                Color acc = RarityAccent((PetRarity)r);
                tmpPets.Sort((a, b) => string.CompareOrdinal(PetUi.NameOf(a), PetUi.NameOf(b)));
                int n = tmpPets.Count;

                var g = UiKit.MakeIcon(contentsRt, UiIcon.Gem, 34f, "HeadGem");
                TLC(g.rectTransform, 30f, y + 22f, 34f, 34f);
                g.color = acc;
                var hl = UiKit.MakeLabel(contentsRt, PetText.Rarity((PetRarity)r)
                    + "  <size=65%><color=#" + Hex(Dim) + ">шанс " + v.ToString("0.#", CultureInfo.InvariantCulture) + "%  ·  "
                    + n + " " + UiDraw.Plural(n, "вид", "вида", "видов") + "</color></size>", 28f, acc, TextAlignmentOptions.Left, true);
                TL(hl.rectTransform, 56f, y, 760f, 44f);
                y += ContHeadH;

                string each = (v / n).ToString("0.##", CultureInfo.InvariantCulture) + "%";
                for (int i = 0; i < n; i++)
                {
                    float x = left + (i % ContCols) * ContPitchX;
                    float cy = y + (i / ContCols) * ContRowH;
                    PetData d = tmpPets[i];

                    var cv = PetCardView.Create(contentsRt, ContCardW, ContCardH, "Pet", false);
                    Anch(cv.rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                         new Vector2(x + ContCardW * 0.5f, -(cy + ContCardH * 0.5f)), new Vector2(ContCardW, ContCardH));
                    cv.Set(d);
                    contentCards.Add(cv);

                    cv.frame.raycastTarget = true;
                    var btn = cv.frame.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.targetGraphic = cv.frame;
                    PetCardView capCard = cv;
                    PetData capPet = d;
                    btn.onClick.AddListener(() => SelectPet(capCard, capPet));

                    var cl = UiKit.MakeLabel(contentsRt, each, 22f, acc, TextAlignmentOptions.Center, true);
                    TL(cl.rectTransform, x, cy + ContCardH + 4f, ContCardW, 30f);
                }
                y += Mathf.Ceil(n / (float)ContCols) * ContRowH + 6f;
            }
        }

        if (!any)
        {
            var none = UiKit.MakeLabel(contentsRt, "Питомцы недоступны: в базе нет подходящих видов", 26f, UiKit.Warn, TextAlignmentOptions.Center, true);
            TL(none.rectTransform, 0f, y + 40f, inW, 40f);
            y += 90f;
        }
        else
        {
            var note = UiKit.MakeLabel(contentsRt, "Шанс на вид указан без учёта гарантии Редкого", 20f, Dim, TextAlignmentOptions.Center, false);
            TL(note.rectTransform, 0f, y, inW, 30f);
            y += 34f;
        }

        contentsRt.sizeDelta = new Vector2(0f, y + 8f);
        contentsRt.anchoredPosition = Vector2.zero;
    }

    // ------------------------------------------------------------ экран «Шансы»: таблица GameBalance.PetCrateOdds по всем клетям
    readonly TMP_Text[,] oddsCell = new TMP_Text[4, 3];
    readonly Image[] oddsColBg = new Image[3];
    TMP_Text oddsNote;

    void OpenOdds()
    {
        if (step != Step.Prepare) return;
        RefreshOddsTable();
        SetStep(Step.Odds);
        SfxManager.Play("hint", 0.5f, 1.2f);
    }

    void CloseOdds()
    {
        if (step != Step.Odds) return;
        SetStep(Step.Prepare);
        Refresh();
    }

    void BuildOdds(Transform p)
    {
        var head = UiKit.MakeLabel(p, "Шансы выпадения", 30f, Color.white, TextAlignmentOptions.Center, true);
        TL(head.rectTransform, 0f, 22f, InW, 40f);

        const float labW = 260f, colW = 200f, rowH = 70f, top = 140f;
        float x0 = (InW - (labW + 3f * colW)) * 0.5f;

        for (int c = 0; c < 3; c++)
        {
            oddsColBg[c] = UiKit.MakeImage(p, "ColBg" + c, UiKit.Slot, true, 0.6f);
            TL(oddsColBg[c].rectTransform, x0 + labW + c * colW + 4f, top - 54f, colW - 8f, 54f + 4f * rowH + 8f);
            oddsColBg[c].raycastTarget = false;

            var t = UiKit.MakeLabel(p, PetText.Crate((CrateType)c), 24f, Color.white, TextAlignmentOptions.Center, true);
            TL(t.rectTransform, x0 + labW + c * colW + 4f, top - 48f, colW - 8f, 44f);
            PetUi.AutoSize(t, 14f, 24f);
            t.color = PetUi.CrateAccent((CrateType)c);
        }

        for (int r = 0; r < 4; r++)
        {
            PetRarity rar = (PetRarity)r;
            var gem = UiKit.MakeIcon(p, UiIcon.Gem, 36f, "Gem" + r);
            gem.color = RarityAccent(rar);
            TLC(gem.rectTransform, x0 + 28f, top + r * rowH + rowH * 0.5f, 36f, 36f);
            var name = UiKit.MakeLabel(p, PetText.Rarity(rar), 28f, RarityAccent(rar), TextAlignmentOptions.Left, true);
            TL(name.rectTransform, x0 + 56f, top + r * rowH, labW - 60f, rowH);
            PetUi.AutoSize(name, 16f, 28f);

            for (int c = 0; c < 3; c++)
            {
                var v = UiKit.MakeLabel(p, "", 32f, Color.white, TextAlignmentOptions.Center, true);
                TL(v.rectTransform, x0 + labW + c * colW + 4f, top + r * rowH, colW - 8f, rowH);
                oddsCell[r, c] = v;
            }
        }

        oddsNote = UiKit.MakeLabel(p, "", 24f, Soft, TextAlignmentOptions.Center, false);
        TL(oddsNote.rectTransform, 40f, top + 4f * rowH + 26f, InW - 80f, 70f);
        oddsNote.enableWordWrapping = true;

        TMP_Text tmpL;
        MakeBtn(p, "Назад", UiBtn.Green, (InW - 300f) * 0.5f, 530f, 300f, 76f, 32f, CloseOdds, out tmpL);
    }

    void RefreshOddsTable()
    {
        for (int c = 0; c < 3; c++)
        {
            float[] odds = GameBalance.PetCrateOdds[c];
            bool cur = (int)crate == c;
            oddsColBg[c].color = cur ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.55f);
            for (int r = 0; r < 4; r++)
            {
                float v = r < odds.Length ? odds[r] : 0f;
                var t = oddsCell[r, c];
                if (v <= 0f) { Set(t, "—"); t.color = Dim; }
                else
                {
                    Set(t, v.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                    t.color = cur ? RarityAccent((PetRarity)r) : Soft;
                }
            }
        }

        var pm = PetManager.Instance;
        string guarantee = "Гарантия: каждая " + GameBalance.PetGuaranteeEvery + "-я клеть одного типа без Редкого даёт Редкого или выше.";
        if (pm != null) guarantee += "\nДля этой клети: <b>До Редкого: " + pm.OpensUntilGuarantee(crate) + "</b>";
        Set(oddsNote, guarantee);
    }

    void SelectPet(PetCardView card, PetData d)
    {
        if (selectedCard != null) selectedCard.rt.localScale = Vector3.one;
        selectedCard = card;

        bool has = d != null;
        detailGem.gameObject.SetActive(has);
        detailBadgeIcon.transform.parent.gameObject.SetActive(has);
        if (!has)
        {
            detailName.color = Dim;
            Set(detailName, "Нажми на питомца: покажу, что он усиливает");
            Set(detailStat, "");
            return;
        }

        card.rt.localScale = new Vector3(1.06f, 1.06f, 1f);
        Color acc = RarityAccent(d.rarity);
        detailGem.color = acc;
        detailBadgeIcon.sprite = UiKit.Icon(PetUi.StatIcon(d.stat));
        detailName.color = acc;
        Set(detailName, PetUi.NameOf(d));
        Set(detailStat, PetUi.StatLine(d));
        SfxManager.Play("ui", 0.3f, 1.2f);
    }

    // ------------------------------------------------------------ мелкие помощники
    static Button MakeBtn(Transform parent, string label, UiBtn style, float x, float y, float w, float h, float font,
                          UnityAction onClick, out TMP_Text labelText)
    {
        var b = UiKit.MakeButton(parent, label, style, new Vector2(w, h), font, 1f);
        TL(b.GetComponent<RectTransform>(), x, y, w, h);
        b.onClick.AddListener(onClick);
        labelText = b.GetComponentInChildren<TMP_Text>();
        if (labelText != null) PetUi.AutoSize(labelText, 16f, font);
        return b;
    }

    static void AddBtn(Image img, UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(a);
    }

    static void Anch(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        PetUi.Anchor(r, anchor, pivot, pos, size);
    }

}
