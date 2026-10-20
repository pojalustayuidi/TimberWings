// Assets/_Project/Scripts/UI/AchievementsWindow.cs (НЕ в Editor)
// Окно «Достижения» («Дневник Лео») в духе Minecraft: вкладки-разделы и дерево достижений, которое можно двигать мышью или пальцем.
// Стиль UiKit, всё строится из кода. Кнопка с кубком в HUD (слева от кнопки питомцев) или клавиша K.
//   AchievementsWindow.Open() / Close() / Toggle() / IsOpen / Available
//
// Управление: тянуть дерево мышью/пальцем (колесо тоже листает), клик по узлу выбирает его и показывает карточку снизу,
//             «Забрать» в карточке или «Забрать все» вверху, вкладки кнопками или стрелками влево/вправо и Tab,
//             закрыть: K, E, Esc или крестик.
// Что видно в дереве:
//   * корни вкладок и узлы, чей родитель открыт: иконка, название, круговая полоска прогресса вокруг медальона
//   * узлы за закрытым родителем: тёмные, с замком и «???» (интрига, как в Minecraft)
//   * готовое к выдаче: пульсирующее свечение, красный «!»; золотое ещё и с вращающимися лучами
//   * полученное: зелёная галочка; линии между открытыми узлами подсвечены цветом вкладки
// Окно только читает AchievementManager и слушает его события, поэтому обновляется само.
// Пока окно открыто, MobileInput.ModalOpen = true (там уже есть строка про AchievementsWindow.IsOpen).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;
using static AchUi;

public class AchievementsWindow : MonoBehaviour
{
    // ============================================================ статический API
    static AchievementsWindow instance;
    public static AchievementsWindow Instance { get { return instance; } }
    public static bool IsOpen { get { return instance != null && instance.open; } }

    // кубок в HUD и клавиша K появляются, когда игрок уже что-то сделал (2-й уровень или есть открытое достижение)
    public static bool Available
    {
        get
        {
            var m = AchievementManager.Instance;
            return m != null && PlayerStats.Instance != null && (PlayerStats.Level >= 2 || m.UnlockedCount > 0);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { Ensure(); }

    static AchievementsWindow Ensure()
    {
        if (instance == null)
        {
            var go = new GameObject("AchievementsWindow");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AchievementsWindow>();
        }
        return instance;
    }

    public static void Open() { Ensure().DoOpen(); }
    public static void Close() { if (instance != null) instance.DoClose(); }
    public static void Toggle() { if (IsOpen) Close(); else Open(); }

    // ============================================================ размеры (единицы Canvas 1920x1080)
    const float WinW = 1400f, WinH = 900f, BS = 1.5f;
    const float InW = WinW - 78f, InH = WinH - 39f - 54f;
    const float TopH = 64f;                                          // полоса вкладок
    const float ViewX = 20f, ViewY = TopH + 6f, ViewW = InW - 40f, ViewH = InH - ViewY - 16f;
    const float VpW = ViewW - 16f, VpH = ViewH - 16f;                // видимая область дерева
    const float ColW = 176f, RowH = 140f, PadX = 96f, PadY = 84f;
    const float TitleReserve = 160f;                                 // сверху место под надпись раздела: узлы её не перекрывают    // шаг сетки и поля дерева
    const float DetW = 980f, DetH = 128f;                            // карточка выбранного достижения
    const float OutT = 14f, FillT = 8f;                              // толщина линий: контур и заливка
    const float PatTile = 288f, PatFarTile = 170f;                                     // сколько единиц Canvas занимает один тайл узора
    const float HudBtn = UpgradeWindow.HudBtnH;

    static readonly Color Soft = PetUi.Soft, Dim = PetUi.Dim;
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);
    static readonly Color DotColor = new Color32(0xE8, 0x4B, 0x3C, 255);
    static readonly Vector2 TLa = new Vector2(0f, 1f), Ctr = new Vector2(0.5f, 0.5f);

    class Node
    {
        public AchievementDef def;
        public Node parentNode;
        public RectTransform rt;
        public Image halo, rays, sel, medal, ringTrack, ring, icon, lockImg, badge, check;
        public TMP_Text label;
        public Vector2 pos;                                  // центр в координатах дерева (x вправо, y ВНИЗ)
        public float size, hoverK, selK, delay, phase;
        public bool vis, unl, clm, can;                      // состояние (обновляет Refresh)
    }

    class Edge
    {
        public Node a, b;
        public Image[] line = new Image[6];
        public int rank = -1;
        public Image flowA, flowB;                      // бегущие огоньки по открытой ветке
        public Vector2 p0, p1, p2, p3;                  // путь линии в координатах дерева (y вниз)
        public float len, phase;
    }   // 3 контура и 3 заливки (по горизонтали, вертикали, горизонтали)

    class TabBtn { public Button btn; public Image img; public TMP_Text label; public RectTransform dot; public TMP_Text dotLabel; }

    bool open, built, dirty = true, hudRegistered, focusing;
    int openFrame, closeFrame = -1;
    float openK, dotT, nextRefresh, tabT, time, detK, detPunch, contentW, contentH;
    Vector2 focusTarget;
    AchCat cat = AchCat.Camp;
    Node sel, hov;

    CanvasGroup overlay, hudGroup;
    RectTransform panel, contentsRt, hudRt, hudDot, detRt;
    ScrollRect scroll;
    RawImage patRaw;
    Image baseImg;
    TMP_Text countText, hintText, claimAllLabel, bgTitle, bgSub;
    Button claimAllBtn;
    Image hudIcon;
    TMP_Text dotLabel;
    AchSparks sparks;
    RawImage patFar;
    Image bgGlow, vignette;
    RectTransform glowRt;
    AchMotes motes;
    Color tintCur, tintTarget;
    bool tintInit;
    float glowKick;
    Vector2 mouseSm;

    readonly List<Node> nodes = new List<Node>();
    readonly List<Node>[] byCat = new List<Node>[AchCats.Count];
    readonly List<Edge>[] edgesByCat = new List<Edge>[AchCats.Count];
    readonly RectTransform[] catRoots = new RectTransform[AchCats.Count];
    readonly TabBtn[] tabs = new TabBtn[AchCats.Count];

    // карточка выбранного
    Image dMedal, dIcon, dLock, dCoinIcon, dCheck, dBarFill, dAccent;
    TMP_Text dTitle, dDesc, dProg, dCoins, dXp, dDone, dClaimLabel;
    Button dClaim;
    RectTransform dBar;

    // ============================================================ открыть / закрыть
    void DoOpen()
    {
        if (open || Time.frameCount == closeFrame) return;
        if (MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return;
        var m = AchievementManager.Instance;
        if (m == null) return;
        EnsureBuilt();
        if (!built) return;
        open = true;
        openFrame = Time.frameCount;
        int ci = FirstClaimCat(m);                              // сразу ведём туда, где ждёт награда
        if (ci >= 0) cat = (AchCat)ci;
        ApplyCat();
        SfxManager.Play("hint", 0.5f, 1.2f);
    }

    void DoClose()
    {
        if (!open) return;
        open = false;
        closeFrame = Time.frameCount;
        hov = null;
        SaveSystem.RequestSave("achievements");
        SfxManager.Play("ui", 0.3f, 0.9f);
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
        var u = UpgradeWindow.Instance; if (u != null && u.IsOpen) return true;
        var p = PlankWorkshopWindow.Instance; if (p != null && p.IsOpen) return true;
        var sw = SellWindow.Instance; if (sw != null && sw.IsOpen) return true;
        var ww = WorkersWindow.Instance; if (ww != null && ww.IsOpen) return true;
        var bw = BuildWindow.Instance; if (bw != null && bw.IsOpen) return true;
        if (PetCrateWindow.IsOpen || PetCollectionWindow.IsOpen) return true;
        return HudMinimap.BigMapOpen || EndingSequence.Active || SettingsMenu.IsOpen;
    }

    void OnEnable() { AchievementManager.Changed += MarkDirty; }
    void OnDisable() { AchievementManager.Changed -= MarkDirty; }
    void MarkDirty() { dirty = true; }

    static int FirstClaimCat(AchievementManager m)
    {
        var list = AchievementDefs.All;
        for (int i = 0; i < list.Count; i++) if (m.CanClaim(list[i].id)) return (int)list[i].cat;
        return -1;
    }

    // ============================================================ цикл
    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;

        if (hudGroup == null && Available) BuildHud();
        UpdateHud(dt);

        if (Input.GetKeyDown(KeyCode.K) && !open && Available) DoOpen();

        if (built && open)
        {
            if (DialogueManager.IsBusy || DialogueSequence.AnyPlaying || OtherWindowOpen() || AchievementManager.Instance == null)
                DoClose();
            else
            {
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);   // Enter и Пробел не жмут кнопки
                if (Time.frameCount != openFrame)
                {
                    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(MobileInput.ActionKey)) DoClose();
                    else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Tab)) StepCat(1);
                    else if (Input.GetKeyDown(KeyCode.LeftArrow)) StepCat(-1);
                }
            }
        }

        if (!built) return;
        openK = Mathf.MoveTowards(openK, open ? 1f : 0f, dt / 0.18f);
        bool show = openK > 0.001f;
        if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
        if (!show) return;

        overlay.alpha = openK;
        panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, UiDraw.EaseOutBack(openK));
        if (!open) return;

        // прогресс живой: обновляем раз в 0.25 с, пока окно открыто
        if (dirty || Time.unscaledTime >= nextRefresh) { dirty = false; nextRefresh = Time.unscaledTime + 0.25f; Refresh(); }

        TickWheel();
        TickFocus(dt);
        TickBackground();
        TickNodes(dt);
        TickFlow();
        TickDetail(dt);
    }

    // ------------------------------------------------------------ колесо: если дерево шире окна и не выше, листаем вбок
    void TickWheel()
    {
        float w = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(w, 0f)) return;
        if (contentW > VpW + 1f && contentH <= VpH + 1f)
        {
            focusing = false;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition - w * 0.12f);
        }
    }

    // ------------------------------------------------------------ плавный «наезд» камеры на узел
    void TickFocus(float dt)
    {
        if (!focusing) return;
        if (Input.GetMouseButton(0) || Input.touchCount > 0) { focusing = false; return; }   // игрок взял дерево в руки
        scroll.StopMovement();
        Vector2 cur = contentsRt.anchoredPosition;
        Vector2 next = Vector2.Lerp(cur, focusTarget, 1f - Mathf.Exp(-9f * dt));
        if ((next - focusTarget).sqrMagnitude < 0.25f) { next = focusTarget; focusing = false; }
        contentsRt.anchoredPosition = next;
    }

    void FocusOn(Node n)
    {
        if (n == null) return;
        float maxX = Mathf.Max(0f, contentW - VpW), maxY = Mathf.Max(0f, contentH - VpH);
        float x = -Mathf.Clamp(n.pos.x - VpW * 0.5f, 0f, maxX);
        float y = Mathf.Clamp(n.pos.y - (VpH - DetH - 30f) * 0.5f, 0f, maxY);   // центр над карточкой
        focusTarget = new Vector2(x, y);
        focusing = true;
    }

    // ------------------------------------------------------------ фон: слои с параллаксом, реакция на мышь, дыхание свечения
    void TickBackground()
    {
        float dt = Time.unscaledDeltaTime;
        tintCur = Color.Lerp(tintCur, tintTarget, 1f - Mathf.Exp(-9f * dt));
        glowKick = Mathf.MoveTowards(glowKick, 0f, dt / 0.7f);
        ApplyTint();

        // мышь: фон чуть «отъезжает» от курсора, как у стекла с глубиной
        Vector2 target = Vector2.zero, loc;
        var vpRt = (RectTransform)patRaw.rectTransform.parent;
        if (!Application.isMobilePlatform && RectTransformUtility.ScreenPointToLocalPointInRectangle(vpRt, Input.mousePosition, null, out loc))
            target = new Vector2(Mathf.Clamp(loc.x / (VpW * 0.5f), -1f, 1f), Mathf.Clamp(loc.y / (VpH * 0.5f), -1f, 1f));
        mouseSm = Vector2.Lerp(mouseSm, target, 1f - Mathf.Exp(-5f * dt));

        Vector2 cam = contentsRt.anchoredPosition;
        Vector2 far = cam * 0.16f - mouseSm * 8f;       // дальний слой: медленный
        Vector2 near = cam * 0.42f - mouseSm * 18f;     // ближний слой: быстрее
        patFar.uvRect = new Rect(-far.x / PatFarTile + 0.37f, -far.y / PatFarTile + 0.21f, VpW / PatFarTile, VpH / PatFarTile);
        patRaw.uvRect = new Rect(-near.x / PatTile, -near.y / PatTile, VpW / PatTile, VpH / PatTile);

        // большое мягкое свечение цвета вкладки: плывёт и дышит, на смене вкладки вспыхивает
        Vector2 gp = new Vector2(VpW * 0.10f, -VpH * 0.04f) + cam * 0.28f - mouseSm * 26f;
        glowRt.anchoredPosition = gp;
        float breathe = 0.5f + 0.5f * Mathf.Sin(time * 0.8f);
        bgGlow.color = new Color(tintCur.r, tintCur.g, tintCur.b, 0.14f + 0.07f * breathe + 0.26f * glowKick);
        glowRt.localScale = Vector3.one * (1f + 0.04f * breathe + 0.12f * glowKick);

        motes.cam = cam;
        motes.mouse = mouseSm;
    }

    void ApplyTint()
    {
        Color t = tintCur;
        baseImg.color = Color.Lerp(UiKit.Ink, t, 0.16f);
        Color pc = Color.Lerp(Color.white, t, 0.6f);
        patRaw.color = pc;
        pc.a = 0.55f;
        patFar.color = pc;
        bgTitle.color = new Color(t.r, t.g, t.b, 0.30f);
        bgSub.color = new Color(t.r, t.g, t.b, 0.80f);
        motes.SetColor(t);
    }

    // ------------------------------------------------------------ анимация узлов
    void TickNodes(float dt)
    {
        tabT += dt;
        var list = byCat[(int)cat];
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            Color tc = TierColor(n.def.tier);

            // появление: узлы выпрыгивают по очереди слева направо
            float ap = Ph(tabT, n.delay, n.delay + 0.38f);
            float appear = ap <= 0f ? 0f : UiDraw.EaseOutBack(ap);

            n.hoverK = Mathf.MoveTowards(n.hoverK, (n == hov && n.vis) ? 1f : 0f, dt / 0.12f);
            n.selK = Mathf.MoveTowards(n.selK, n == sel ? 1f : 0f, dt / 0.15f);
            n.rt.localScale = Vector3.one * (appear * (1f + 0.10f * n.hoverK + 0.05f * n.selK));

            // лёгкое парение: у готовых к выдаче сильнее, у закрытых почти нет
            float bob = Mathf.Sin(time * 1.35f + n.phase) * (n.can ? 4.5f : (n.vis ? 2.2f : 0.8f));
            n.rt.anchoredPosition = new Vector2(n.pos.x, -n.pos.y + bob * appear);

            // выбранный: яркое кольцо
            n.sel.color = new Color(1f, 0.96f, 0.82f, n.selK);

            // готово к выдаче: свечение пульсирует, значок покачивается
            float haloA = 0f;
            if (n.can)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f + n.phase);
                haloA = Mathf.Lerp(0.35f, 0.95f, pulse);
                float wob = Mathf.Sin(time * 2.2f + n.phase);
                float kick = Mathf.Clamp01(wob * 3f - 1.2f);             // короткое покачивание раз в такт
                n.icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(time * 14f + n.phase) * 9f * kick);
                n.badge.rectTransform.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(time * 6f + n.phase));
            }
            else
            {
                haloA = n.hoverK * 0.35f + n.selK * 0.35f;
                n.icon.rectTransform.localEulerAngles = Vector3.zero;
            }
            n.halo.color = new Color(tc.r, tc.g, tc.b, haloA);
            n.halo.gameObject.SetActive(haloA > 0.01f);

            bool rays = n.can && n.def.tier == AchTier.Gold;
            n.rays.gameObject.SetActive(rays);
            if (rays)
            {
                n.rays.color = new Color(tc.r, tc.g, tc.b, 0.6f);
                n.rays.rectTransform.Rotate(0f, 0f, -22f * dt);
            }
        }
    }

    // ------------------------------------------------------------ огоньки бегут по открытым веткам: яркие по пройденным, бледные к следующей цели
    void TickFlow()
    {
        var edges = edgesByCat[(int)cat];
        Color tint = tintCur;
        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            bool on = e.rank >= 1 && e.len > 1f && tabT > 0.45f;
            e.flowA.gameObject.SetActive(on);
            e.flowB.gameObject.SetActive(on && e.rank == 2);
            if (!on) continue;
            bool full = e.rank == 2;
            Color c = full ? Color.Lerp(UiKit.BrassHi, Color.white, 0.35f) : Color.Lerp(tint, Color.white, 0.4f);
            float baseA = full ? 0.95f : 0.45f;
            float sz = full ? 30f : 22f;
            float d = Mathf.Repeat(time * 150f + e.phase * e.len, e.len);
            PlaceFlow(e.flowA, e, d, c, baseA, sz);
            if (full) PlaceFlow(e.flowB, e, Mathf.Repeat(d + e.len * 0.5f, e.len), c, baseA, sz);
        }
    }

    static void PlaceFlow(Image img, Edge e, float d, Color c, float a, float size)
    {
        Vector2 p = PointAt(e, d);
        float k = Mathf.Sin(Mathf.Clamp01(d / e.len) * Mathf.PI);
        c.a = a * Mathf.Sqrt(k);
        img.color = c;
        img.rectTransform.anchoredPosition = new Vector2(p.x, -p.y);
        img.rectTransform.sizeDelta = new Vector2(size, size);
    }

    static Vector2 PointAt(Edge e, float d)
    {
        float l0 = (e.p1 - e.p0).magnitude, l1 = (e.p2 - e.p1).magnitude, l2 = (e.p3 - e.p2).magnitude;
        if (d <= l0) return l0 > 0f ? Vector2.Lerp(e.p0, e.p1, d / l0) : e.p0;
        d -= l0;
        if (d <= l1) return l1 > 0f ? Vector2.Lerp(e.p1, e.p2, d / l1) : e.p1;
        d -= l1;
        return l2 > 0f ? Vector2.Lerp(e.p2, e.p3, Mathf.Min(1f, d / l2)) : e.p3;
    }

    // ------------------------------------------------------------ карточка выбранного: выезжает снизу
    void TickDetail(float dt)
    {
        detK = Mathf.MoveTowards(detK, sel != null ? 1f : 0f, dt / 0.22f);
        detPunch = Mathf.MoveTowards(detPunch, 0f, dt / 0.25f);
        float y = Mathf.LerpUnclamped(-(DetH + 40f), 16f, UiDraw.EaseOutBack(detK));
        detRt.anchoredPosition = new Vector2(0f, y);
        detRt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(detPunch * Mathf.PI));
        detRt.gameObject.SetActive(detK > 0.001f);
    }

    // ============================================================ вкладки
    void StepCat(int dir)
    {
        int n = AchCats.Count;
        SetCat(((int)cat + dir + n) % n);
    }

    void SetCat(int ci)
    {
        if ((int)cat == ci) return;
        cat = (AchCat)ci;
        ApplyCat();
        SfxManager.Play("ui", 0.3f, 1.1f);
    }

    // показать выбранную вкладку: раскладка, камера, выбор «следующей цели»
    void ApplyCat()
    {
        int ci = (int)cat;
        for (int i = 0; i < catRoots.Length; i++) catRoots[i].gameObject.SetActive(i == ci);
        LayoutCat(ci);

        tintTarget = AchCats.Colors[ci];
        if (!tintInit) { tintInit = true; tintCur = tintTarget; }
        patRaw.texture = Pattern(cat);
        patFar.texture = Pattern(cat);
        Set(bgTitle, AchCats.Names[ci].ToUpperInvariant());
        Set(bgSub, AchCats.Blurbs[ci]);
        motes.SetStyle(ci);
        glowKick = 1f;
        ApplyTint();

        contentsRt.anchoredPosition = Vector2.zero;
        scroll.StopMovement();
        tabT = 0f;
        hov = null;
        focusing = false;
        Refresh();
        PinDefault();
    }

    // ближайшая цель: сначала то, что можно забрать, потом самое «горячее» по прогрессу
    void PinDefault()
    {
        var list = byCat[(int)cat];
        Node best = null;
        float bestK = -1f;
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            if (!n.vis) continue;
            if (n.can) { best = n; break; }
            if (n.unl) continue;
            var m = AchievementManager.Instance;
            float k = (m != null && n.def.goal > 0) ? Mathf.Clamp01((float)m.Progress(n.def) / n.def.goal) : 0f;
            if (k > bestK) { bestK = k; best = n; }
        }
        if (best == null && list.Count > 0) best = list[0];
        Select(best, true, false);
    }

    void Select(Node n, bool focus, bool sound)
    {
        sel = n;
        dirty = true;
        if (n == null) return;
        if (focus) FocusOn(n);
        if (sound) SfxManager.Play("ui", 0.3f, n.vis ? 1.25f : 0.8f);
        FillDetail(n);
    }

    // ============================================================ данные -> окно
    void Refresh()
    {
        var m = AchievementManager.Instance;
        if (m == null) return;

        Set(countText, m.UnlockedCount + " / " + m.Total);

        // узлы
        for (int i = 0; i < nodes.Count; i++) Skin(nodes[i], m);

        // линии: ярко, если открыты оба конца; мягко, если открыт только родитель; тускло, если путь ещё закрыт
        for (int c = 0; c < edgesByCat.Length; c++)
        {
            Color tint = AchCats.Colors[c];
            var edges = edgesByCat[c];
            bool resort = false;
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                Color fill;
                int rank;
                if (e.a.unl && e.b.unl) { fill = Color.Lerp(UiKit.BrassHi, tint, 0.35f); rank = 2; }
                else if (e.a.unl) { fill = Color.Lerp(new Color(0.42f, 0.48f, 0.60f, 1f), tint, 0.45f); rank = 1; }
                else { fill = new Color(0.17f, 0.21f, 0.30f, 1f); rank = 0; }
                if (e.rank != rank) { e.rank = rank; resort = true; }
                for (int k = 0; k < 3; k++)
                {
                    e.line[k].color = new Color(AchUi.Outline.r, AchUi.Outline.g, AchUi.Outline.b, 0.9f);
                    e.line[3 + k].color = fill;
                }
            }
            // общий «ствол» нескольких веток: яркая линия всегда сверху тусклой
            if (resort)
                for (int r = 0; r <= 2; r++)
                    for (int i = 0; i < edges.Count; i++)
                        if (edges[i].rank == r)
                            for (int k = 3; k < 6; k++) edges[i].line[k].transform.SetAsLastSibling();
        }

        // вкладки: счётчик готовых наград красной точкой
        for (int c = 0; c < tabs.Length; c++)
        {
            int unl = 0, claim = 0, total = byCat[c].Count;
            for (int i = 0; i < byCat[c].Count; i++) { if (byCat[c][i].unl) unl++; if (byCat[c][i].can) claim++; }
            var tb = tabs[c];
            Set(tb.label, AchCats.Names[c] + "  " + unl + "/" + total);
            tb.img.color = c == (int)cat ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
            tb.btn.transform.localScale = Vector3.one * (c == (int)cat ? 1.04f : 1f);
            tb.dot.gameObject.SetActive(claim > 0);
            if (claim > 0) Set(tb.dotLabel, claim > 9 ? "9+" : claim.ToString());
        }

        int claimable = m.ClaimableCount;
        claimAllBtn.interactable = claimable > 0;
        Set(claimAllLabel, claimable > 0 ? "Забрать все (" + claimable + ")" : "Забрать все");
        Set(hintText, "K или Esc: закрыть  ·  тяни дерево мышью  ·  стрелки: вкладки");

        if (sel != null) FillDetail(sel);
    }

    // внешний вид одного узла по состоянию
    void Skin(Node n, AchievementManager m)
    {
        var d = n.def;
        n.unl = m.IsUnlocked(d.id);
        n.vis = n.unl || n.parentNode == null || m.IsUnlocked(n.parentNode.def.id);   // выполненное видно всегда, даже если родитель ещё нет
        n.clm = m.IsClaimed(d.id);
        n.can = m.CanClaim(d.id);
        Color tc = TierColor(d.tier);

        n.lockImg.gameObject.SetActive(!n.vis);
        n.icon.gameObject.SetActive(n.vis);
        n.badge.gameObject.SetActive(n.vis && n.can);
        n.check.gameObject.SetActive(n.vis && n.clm);

        if (!n.vis)
        {
            n.medal.color = new Color(0.17f, 0.20f, 0.28f, 1f);
            n.ring.gameObject.SetActive(false);
            n.ringTrack.gameObject.SetActive(false);
            Set(n.label, "???");
            n.label.color = Dim;
            return;
        }

        Set(n.label, d.title);
        n.label.color = n.unl ? Color.white : new Color(0.82f, 0.86f, 0.92f, 1f);

        if (n.unl)
        {
            n.medal.color = n.clm ? Color.Lerp(tc, new Color(0.25f, 0.25f, 0.3f, 1f), 0.3f) : tc;
            n.icon.color = n.clm ? new Color(1f, 1f, 1f, 0.9f) : Color.white;
            n.ring.gameObject.SetActive(false);
            n.ringTrack.gameObject.SetActive(false);
        }
        else
        {
            n.medal.color = new Color(tc.r * 0.40f, tc.g * 0.40f, tc.b * 0.40f, 1f);
            n.icon.color = new Color(1f, 1f, 1f, 0.55f);
            float k = d.goal > 0 ? Mathf.Clamp01((float)m.Progress(d) / d.goal) : 0f;
            n.ringTrack.gameObject.SetActive(true);
            n.ring.gameObject.SetActive(k > 0.002f);
            n.ring.fillAmount = k;
            n.ring.color = tc;
        }
    }

    // карточка выбранного достижения
    void FillDetail(Node n)
    {
        var m = AchievementManager.Instance;
        if (m == null || n == null) return;
        var d = n.def;
        Color tc = TierColor(d.tier);
        dAccent.color = n.vis ? tc : new Color(tc.r * 0.45f, tc.g * 0.45f, tc.b * 0.45f, 1f);

        if (!n.vis)
        {
            dMedal.color = new Color(0.17f, 0.20f, 0.28f, 1f);
            dIcon.gameObject.SetActive(false);
            dLock.gameObject.SetActive(true);
            Set(dTitle, "???");
            string pt = n.parentNode != null ? n.parentNode.def.title : "";
            Set(dDesc, "Откроется после достижения «" + pt + "»");
            dBar.gameObject.SetActive(false);
            Set(dProg, TierName(d.tier));
            dProg.color = tc;
            dCoinIcon.gameObject.SetActive(false);
            dCoins.gameObject.SetActive(false);
            dXp.gameObject.SetActive(false);
            dClaim.gameObject.SetActive(false);
            dCheck.gameObject.SetActive(false);
            dDone.gameObject.SetActive(false);
            return;
        }

        dMedal.color = n.unl ? tc : new Color(tc.r * 0.45f, tc.g * 0.45f, tc.b * 0.45f, 1f);
        dIcon.gameObject.SetActive(true);
        dIcon.sprite = UiKit.Icon(d.icon);
        dIcon.color = n.unl ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        dLock.gameObject.SetActive(false);
        Set(dTitle, d.title);
        Set(dDesc, d.desc);

        int prog = Mathf.Min(m.Progress(d), d.goal);
        dBar.gameObject.SetActive(true);
        dBarFill.fillAmount = d.goal > 0 ? Mathf.Clamp01((float)prog / d.goal) : 0f;
        Set(dProg, ProgText(d, prog));
        dProg.color = Soft;

        dCoinIcon.gameObject.SetActive(d.coins > 0);
        dCoins.gameObject.SetActive(d.coins > 0);
        Set(dCoins, "+" + d.coins);
        dXp.gameObject.SetActive(d.xp > 0);
        Set(dXp, "+" + d.xp + " XP");

        dClaim.gameObject.SetActive(n.can);
        dCheck.gameObject.SetActive(n.clm);
        dDone.gameObject.SetActive(n.clm);
    }

    static string ProgText(AchievementDef d, int prog)
    {
        if (d.metric == AchMetric.PlaySeconds) return Hours(prog) + " / " + Hours(d.goal);
        if (d.metric == AchMetric.Chapter) return prog >= d.goal ? "Готово" : "Не пройдено";
        return prog + " / " + d.goal;
    }

    static string Hours(int sec)
    {
        if (sec >= 3600 * 10) return (sec / 3600) + " ч";
        if (sec >= 3600) return (sec / 3600f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " ч";
        return (sec / 60) + " мин";
    }

    // ============================================================ действия
    void OnNodeClick(Node n)
    {
        if (sel == n) { SfxManager.Play("ui", 0.2f, 1.0f); detPunch = 1f; return; }
        Select(n, false, true);
        detPunch = 1f;
    }

    void OnNodeEnter(Node n)
    {
        hov = n;
        if (n.vis) SfxManager.Play("ui", 0.10f, 1.5f);
    }

    void OnNodeExit(Node n) { if (hov == n) hov = null; }

    void OnClaim()
    {
        var m = AchievementManager.Instance;
        if (m == null || sel == null || !sel.can) return;
        var n = sel;
        if (m.Claim(n.def.id))
        {
            ClaimFx(n);
            SfxManager.Play("hint", 0.6f, 1.5f);
            detPunch = 1f;
        }
    }

    void OnClaimAll()
    {
        var m = AchievementManager.Instance;
        if (m == null) return;
        var list = byCat[(int)cat];
        var fx = new List<Node>();
        int coins = 0;
        for (int i = 0; i < list.Count; i++) if (list[i].can) { fx.Add(list[i]); coins += list[i].def.coins; }
        if (m.ClaimAll() > 0)
        {
            for (int i = 0; i < fx.Count; i++) ClaimFx(fx[i]);
            if (coins > 0 && fx.Count > 1) sparks.FloatText(new Vector2(contentW * 0.5f, -contentH * 0.2f), "+" + coins, UiKit.BrassHi, 54f);
            SfxManager.Play("hint", 0.7f, 1.6f);
            detPunch = 1f;
        }
    }

    // искры и «+30» над узлом
    void ClaimFx(Node n)
    {
        Color tc = TierColor(n.def.tier);
        Vector2 p = new Vector2(n.pos.x, -n.pos.y);
        int count = n.def.tier == AchTier.Gold ? 34 : (n.def.tier == AchTier.Silver ? 24 : 16);
        sparks.Burst(p, tc, count, 360f, 26f, 420f, 0.95f);
        if (n.def.coins > 0) sparks.FloatText(p + new Vector2(0f, n.size * 0.55f), "+" + n.def.coins, UiKit.BrassHi);
        if (n.def.xp > 0) sparks.FloatText(p + new Vector2(0f, n.size * 0.55f - 34f), "+" + n.def.xp + " XP", UiKit.Good, 26f);
    }

    // ============================================================ сборка окна
    void Build()
    {
        if (built) return;
        var cgo = new GameObject("AchievementsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31;
        ConfigScaler(cgo.GetComponent<CanvasScaler>());

        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.62f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;   // затемнение гасит клики по миру, но окно НЕ закрывает (промах мимо окна не должен его сбрасывать)
        overlay = ov.gameObject.AddComponent<CanvasGroup>();

        var win = UiKit.MakePanel(ov.transform, new Vector2(WinW, WinH), true, BS, "Window");
        panel = win.rectTransform;
        Anch(panel, Ctr, Ctr, Vector2.zero, new Vector2(WinW, WinH));
        AddBtn(win, () => { });                                 // клик по окну не «всплывает» до затемнения

        BuildHeader();

        var content = NewRT("Content", panel);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f * BS, 26f * BS);
        content.offsetMax = new Vector2(-26f * BS, -36f * BS);
        var inner = UiKit.MakePanel(content, new Vector2(InW, InH), false, 0.6f, "Inner");
        Stretch(inner.rectTransform);

        BuildTabs(inner.transform);
        BuildGraph(inner.transform);
        BuildDetail(inner.transform);

        hintText = UiKit.MakeLabel(ov.transform, "", 22f, Dim, TextAlignmentOptions.Center, false);
        Anch(hintText.rectTransform, Ctr, new Vector2(0.5f, 1f), new Vector2(0, -(WinH * 0.5f + 12f)), new Vector2(1100, 34));

        ov.gameObject.SetActive(false);
        built = true;
    }

    static void ConfigScaler(CanvasScaler sc)
    {
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
    }

    void BuildHeader()
    {
        var cup = UiKit.MakeIcon(panel, UiIcon.Trophy, 44f, "CupIcon");
        TLC(cup.rectTransform, 66f, 27f, 44f, 44f);
        countText = UiKit.MakeLabel(panel, "0 / 0", 30f, WoodText, TextAlignmentOptions.Left, true);
        TL(countText.rectTransform, 96f, 8f, 220f, 38f);

        var title = UiKit.MakeLabel(panel, "ДОСТИЖЕНИЯ", 34f, WoodText, TextAlignmentOptions.Center, true);
        Anch(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(520, 40));

        var close = UiKit.MakeButton(panel, "", UiBtn.Amber, new Vector2(64, 48), 20f, 1f);
        Anch(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -5), new Vector2(64, 48));
        close.onClick.AddListener(DoClose);
        var x = UiKit.MakeIcon(close.transform, UiIcon.Cross, 30f, "X");
        x.rectTransform.anchoredPosition = new Vector2(0, 2);
    }

    void BuildTabs(Transform p)
    {
        const float W = 196f, H = 48f, Gap = 8f;
        for (int i = 0; i < AchCats.Count; i++)
        {
            int ci = i;
            var tb = new TabBtn();
            tb.btn = UiKit.MakeButton(p, "", UiBtn.Green, new Vector2(W, H), 20f, 1f);
            tb.btn.name = "Tab_" + AchCats.Names[i];
            tb.img = tb.btn.GetComponent<Image>();
            TL(tb.btn.GetComponent<RectTransform>(), 20f + i * (W + Gap), 8f, W, H);
            tb.btn.navigation = new Navigation { mode = Navigation.Mode.None };
            tb.btn.onClick.AddListener(() => SetCat(ci));

            var ic = UiKit.MakeIcon(tb.btn.transform, AchCats.Icons[i], 32f, "Icon");
            TLC(ic.rectTransform, 30f, 22f, 32f, 32f);

            tb.label = tb.btn.GetComponentInChildren<TMP_Text>();
            tb.label.rectTransform.offsetMin = new Vector2(50f, tb.label.rectTransform.offsetMin.y);
            tb.label.rectTransform.offsetMax = new Vector2(-8f, tb.label.rectTransform.offsetMax.y);
            PetUi.AutoSize(tb.label, 13f, 20f);

            var dot = UiKit.MakeImage(tb.btn.transform, "Dot", UiKit.Medallion, false);
            dot.color = DotColor;
            tb.dot = dot.rectTransform;
            Anch(tb.dot, new Vector2(1f, 1f), Ctr, new Vector2(-6f, -4f), new Vector2(28f, 28f));
            tb.dotLabel = UiKit.MakeLabel(dot.transform, "!", 17f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(tb.dotLabel.rectTransform);
            tb.dot.gameObject.SetActive(false);
            tabs[i] = tb;
        }

        claimAllBtn = UiKit.MakeButton(p, "", UiBtn.Yellow, new Vector2(260, 48), 20f, 1f);
        TL(claimAllBtn.GetComponent<RectTransform>(), InW - 20f - 260f, 8f, 260f, 48f);
        claimAllBtn.onClick.AddListener(OnClaimAll);
        claimAllLabel = claimAllBtn.GetComponentInChildren<TMP_Text>();
        if (claimAllLabel != null) PetUi.AutoSize(claimAllLabel, 14f, 20f);
    }

    void BuildGraph(Transform p)
    {
        var view = UiKit.MakeImage(p, "View", UiKit.Slot, true, 1f);
        TL(view.rectTransform, ViewX, ViewY, ViewW, ViewH);

        var vp = NewRT("Viewport", view.transform);
        Stretch(vp);
        vp.offsetMin = new Vector2(8f, 8f);
        vp.offsetMax = new Vector2(-8f, -8f);
        vp.gameObject.AddComponent<RectMask2D>();
        baseImg = vp.gameObject.AddComponent<Image>();
        baseImg.color = UiKit.Navy;
        baseImg.raycastTarget = true;                           // ловит перетаскивание

        // слои фона (снизу вверх): дальний узор, ближний узор, свечение, огоньки, виньетка
        var patF = NewRT("PatternFar", vp);
        Stretch(patF);
        patFar = patF.gameObject.AddComponent<RawImage>();
        patFar.raycastTarget = false;

        var pat = NewRT("Pattern", vp);
        Stretch(pat);
        patRaw = pat.gameObject.AddComponent<RawImage>();
        patRaw.raycastTarget = false;

        bgGlow = UiKit.MakeImage(vp, "BgGlow", PetUi.GlowSprite, false);
        bgGlow.raycastTarget = false;
        glowRt = bgGlow.rectTransform;
        Anch(glowRt, Ctr, Ctr, Vector2.zero, new Vector2(1100f, 760f));

        motes = AchMotes.Create(vp, VpW, VpH);

        vignette = UiKit.MakeImage(vp, "Vignette", AchUi.Vignette, false);
        vignette.raycastTarget = false;
        Stretch(vignette.rectTransform);

        // название вкладки большой тенью в углу, как заставка раздела
        bgTitle = UiKit.MakeLabel(vp, "", 66f, Color.white, TextAlignmentOptions.Left, true);
        Anch(bgTitle.rectTransform, TLa, TLa, new Vector2(26f, -8f), new Vector2(760f, 80f));
        bgTitle.raycastTarget = false;
        bgSub = UiKit.MakeLabel(vp, "", 22f, Color.white, TextAlignmentOptions.Left, false);
        Anch(bgSub.rectTransform, TLa, TLa, new Vector2(30f, -80f), new Vector2(900f, 30f));
        bgSub.raycastTarget = false;

        contentsRt = NewRT("Content", vp);
        Anch(contentsRt, TLa, TLa, Vector2.zero, new Vector2(VpW, VpH));

        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = vp;
        scroll.content = contentsRt;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.1f;

        // узлы и линии каждой вкладки живут в своём корне
        var ids = new Dictionary<string, Node>();
        for (int c = 0; c < AchCats.Count; c++)
        {
            byCat[c] = new List<Node>();
            edgesByCat[c] = new List<Edge>();
            var root = NewRT("Cat_" + AchCats.Names[c], contentsRt);
            Stretch(root);
            catRoots[c] = root;
            var linesOut = NewRT("LinesOut", root); Stretch(linesOut);     // все контуры под всеми заливками
            var linesFill = NewRT("LinesFill", root); Stretch(linesFill);
            var flowLayer = NewRT("Flow", root); Stretch(flowLayer);          // бегущие огоньки: над линиями, под узлами
            var nodeLayer = NewRT("Nodes", root); Stretch(nodeLayer);

            var defs = AchievementDefs.OfCat((AchCat)c);
            for (int i = 0; i < defs.Count; i++)
            {
                var n = BuildNode(nodeLayer, defs[i]);
                n.delay = defs[i].gx * 0.08f + defs[i].gy * 0.03f;
                n.phase = i * 0.9f;
                nodes.Add(n);
                byCat[c].Add(n);
                ids[defs[i].id] = n;
            }
        }

        // связи родитель -> потомок
        for (int c = 0; c < AchCats.Count; c++)
        {
            var linesOut = catRoots[c].Find("LinesOut");
            var linesFill = catRoots[c].Find("LinesFill");
            var flowLayer = catRoots[c].Find("Flow");
            for (int i = 0; i < byCat[c].Count; i++)
            {
                var n = byCat[c][i];
                Node par;
                if (string.IsNullOrEmpty(n.def.parent) || !ids.TryGetValue(n.def.parent, out par)) continue;
                n.parentNode = par;
                var e = new Edge { a = par, b = n };
                for (int k = 0; k < 6; k++)
                {
                    var img = UiKit.MakeImage(k < 3 ? linesOut : linesFill, k < 3 ? "Outline" : "Fill", null, false);
                    img.raycastTarget = false;
                    e.line[k] = img;
                }
                e.flowA = MakeFlow(flowLayer);
                e.flowB = MakeFlow(flowLayer);
                e.phase = ((i * 37 + c * 11) % 100) / 100f;
                edgesByCat[c].Add(e);
            }
        }

        // слой искр поверх дерева
        sparks = AchSparks.Create(contentsRt, TLa);

    }

    Node BuildNode(Transform parent, AchievementDef d)
    {
        var n = new Node { def = d };
        float S = d.tier == AchTier.Gold ? 104f : (d.tier == AchTier.Silver ? 92f : 84f);
        n.size = S;

        n.rt = NewRT("Node_" + d.id, parent);
        Anch(n.rt, TLa, Ctr, Vector2.zero, new Vector2(S, S));

        n.rays = Part(n.rt, "Rays", PetUi.RaysSprite, S * 3.1f);
        n.halo = Part(n.rt, "Halo", PetUi.GlowSprite, S * 2.4f);
        n.sel = Part(n.rt, "Sel", UiKit.Medallion, S * 1.22f);
        n.ringTrack = Part(n.rt, "RingTrack", AchUi.Ring, S * 1.24f);
        n.ringTrack.color = new Color(0f, 0f, 0f, 0.45f);
        n.ring = Part(n.rt, "Ring", AchUi.Ring, S * 1.24f);
        n.ring.type = Image.Type.Filled;
        n.ring.fillMethod = Image.FillMethod.Radial360;
        n.ring.fillOrigin = (int)Image.Origin360.Top;
        n.ring.fillClockwise = true;

        n.sel.color = new Color(1f, 1f, 1f, 0f);
        n.halo.color = new Color(1f, 1f, 1f, 0f);
        n.rays.color = new Color(1f, 1f, 1f, 0f);

        n.medal = Part(n.rt, "Medal", UiKit.Medallion, S);
        n.medal.raycastTarget = true;
        var hit = n.medal.gameObject.AddComponent<AchNodeHit>();
        hit.onEnter = () => OnNodeEnter(n);
        hit.onExit = () => OnNodeExit(n);
        hit.onClick = () => OnNodeClick(n);

        n.icon = Part(n.rt, "Icon", UiKit.Icon(d.icon), S * 0.60f);
        n.icon.preserveAspect = true;
        n.icon.rectTransform.anchoredPosition = new Vector2(0f, 1f);
        n.lockImg = Part(n.rt, "Lock", UiKit.Icon(UiIcon.Lock), S * 0.50f);
        n.lockImg.color = new Color(1f, 1f, 1f, 0.75f);

        n.badge = Part(n.rt, "Badge", UiKit.Medallion, S * 0.38f);
        n.badge.color = DotColor;
        n.badge.rectTransform.anchoredPosition = new Vector2(S * 0.38f, S * 0.38f);
        var bl = UiKit.MakeLabel(n.badge.transform, "!", S * 0.24f, Color.white, TextAlignmentOptions.Center, true);
        Stretch(bl.rectTransform);

        n.check = Part(n.rt, "Check", UiKit.Medallion, S * 0.36f);
        n.check.color = UiKit.Good;
        n.check.rectTransform.anchoredPosition = new Vector2(S * 0.38f, -S * 0.34f);
        var ck = UiKit.MakeIcon(n.check.transform, UiIcon.Check, S * 0.24f, "Tick");
        ck.rectTransform.anchoredPosition = Vector2.zero;

        n.label = UiKit.MakeLabel(n.rt, d.title, 19f, Color.white, TextAlignmentOptions.Top, true);
        Anch(n.label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -(S * 0.12f + 2f)), new Vector2(ColW - 14f, 48f));
        Wrap2(n.label, 12f, 19f);
        Outlined(n.label);
        n.label.raycastTarget = false;
        return n;
    }

    static Image MakeFlow(Transform parent)
    {
        var img = UiKit.MakeImage(parent, "Flow", PetUi.SparkSprite, false);
        img.raycastTarget = false;
        Anch(img.rectTransform, TLa, Ctr, Vector2.zero, new Vector2(26f, 26f));
        img.gameObject.SetActive(false);
        return img;
    }

    // картинка по центру узла
    static Image Part(RectTransform parent, string name, Sprite sprite, float size)
    {
        var img = UiKit.MakeImage(parent, name, sprite, false);
        img.raycastTarget = false;
        Anch(img.rectTransform, Ctr, Ctr, Vector2.zero, new Vector2(size, size));
        return img;
    }

    // ------------------------------------------------------------ раскладка вкладки: позиции узлов, линии, размер содержимого
    void LayoutCat(int ci)
    {
        var list = byCat[ci];
        int maxX = 0, maxY = 0;
        for (int i = 0; i < list.Count; i++) { maxX = Mathf.Max(maxX, list[i].def.gx); maxY = Mathf.Max(maxY, list[i].def.gy); }
        float gw = maxX * ColW, gh = maxY * RowH;
        float need = TitleReserve + gh + 110f + DetH + 24f;      // заголовок + дерево + подписи + карточка снизу
        contentW = Mathf.Max(VpW, gw + 2f * PadX);
        contentH = Mathf.Max(VpH, need);
        float ox = (contentW - gw) * 0.5f;
        float oy = TitleReserve + (contentH - need) * 0.35f;
        contentsRt.sizeDelta = new Vector2(contentW, contentH);

        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            n.pos = new Vector2(ox + n.def.gx * ColW, oy + n.def.gy * RowH);
            n.rt.anchoredPosition = new Vector2(n.pos.x, -n.pos.y);
        }
        var edges = edgesByCat[ci];
        for (int i = 0; i < edges.Count; i++) PlaceEdge(edges[i]);
        sparks.transform.SetAsLastSibling();

        // если дерево целиком помещается по оси, эту ось не тянем (без дрожи на «резинке»)
        scroll.horizontal = contentW > VpW + 1f;
        scroll.vertical = contentH > VpH + 1f;
    }

    static void PlaceEdge(Edge e)
    {
        Vector2 a = e.a.pos, b = e.b.pos;
        float mx = a.x + (b.x - a.x) * 0.5f;
        e.p0 = a; e.p1 = new Vector2(mx, a.y); e.p2 = new Vector2(mx, b.y); e.p3 = b;
        e.len = (e.p1 - e.p0).magnitude + (e.p2 - e.p1).magnitude + (e.p3 - e.p2).magnitude;
        SetSeg(e.line[0], e.line[3], a.x, a.y, mx, a.y);
        SetSeg(e.line[1], e.line[4], mx, a.y, mx, b.y);
        SetSeg(e.line[2], e.line[5], mx, b.y, b.x, b.y);
    }

    static void SetSeg(Image outline, Image fill, float x0, float y0, float x1, float y1)
    {
        float dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        bool on = dx + dy > 0.5f;
        outline.gameObject.SetActive(on);
        fill.gameObject.SetActive(on);
        if (!on) return;
        Vector2 c = new Vector2((x0 + x1) * 0.5f, -(y0 + y1) * 0.5f);
        bool horiz = dy < 0.5f;
        Anch(outline.rectTransform, TLa, Ctr, c, horiz ? new Vector2(dx + OutT, OutT) : new Vector2(OutT, dy + OutT));
        Anch(fill.rectTransform, TLa, Ctr, c, horiz ? new Vector2(dx + FillT, FillT) : new Vector2(FillT, dy + FillT));
    }

    // ------------------------------------------------------------ карточка выбранного
    void BuildDetail(Transform p)
    {
        var det = UiKit.MakeImage(p, "Detail", UiKit.Panel, true, 0.7f);
        detRt = det.rectTransform;
        Anch(detRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -(DetH + 40f)), new Vector2(DetW, DetH));
        AddBtn(det, () => { });                                  // клики по карточке не уходят в дерево

        dAccent = UiKit.MakeImage(detRt, "Accent", null, false);
        dAccent.raycastTarget = false;
        TL(dAccent.rectTransform, 14f, 16f, 6f, DetH - 32f);

        dMedal = UiKit.MakeImage(detRt, "Medal", UiKit.Medallion, false);
        TLC(dMedal.rectTransform, 74f, 64f, 88f, 88f);
        dIcon = UiKit.MakeIcon(dMedal.transform, UiIcon.Star, 54f, "Icon");
        dIcon.rectTransform.anchoredPosition = new Vector2(0f, 1f);
        dLock = UiKit.MakeIcon(dMedal.transform, UiIcon.Lock, 44f, "Lock");
        dLock.gameObject.SetActive(false);

        dTitle = Lbl(detRt, "", 31f, Color.white, TextAlignmentOptions.Left, true, 134f, 12f, 520f, 38f);
        dDesc = Lbl(detRt, "", 21f, Soft, TextAlignmentOptions.Left, false, 134f, 50f, 560f, 30f);

        var bar = UiKit.MakeBar(detRt, UiBar.Amber, new Vector2(360f, 24f), out dBarFill, 0.8f);
        dBar = bar.rectTransform;
        TL(dBar, 134f, 88f, 360f, 24f);
        dProg = Lbl(detRt, "", 20f, Soft, TextAlignmentOptions.Left, true, 508f, 85f, 190f, 30f);

        dCoinIcon = UiKit.MakeIcon(detRt, UiIcon.Coin, 34f, "Coin");
        TLC(dCoinIcon.rectTransform, 730f, 42f, 34f, 34f);
        dCoins = Lbl(detRt, "", 28f, UiKit.BrassHi, TextAlignmentOptions.Left, true, 754f, 24f, 100f, 36f);
        dXp = Lbl(detRt, "", 22f, UiKit.Good, TextAlignmentOptions.Left, true, 714f, 70f, 140f, 30f);

        dClaim = UiKit.MakeButton(detRt, "Забрать", UiBtn.Green, new Vector2(170f, 58f), 24f, 1f);
        TL(dClaim.GetComponent<RectTransform>(), DetW - 196f, 34f, 170f, 58f);
        dClaim.onClick.AddListener(OnClaim);
        dClaimLabel = dClaim.GetComponentInChildren<TMP_Text>();
        if (dClaimLabel != null) PetUi.AutoSize(dClaimLabel, 14f, 24f);

        dCheck = UiKit.MakeIcon(detRt, UiIcon.Check, 36f, "Done");
        TLC(dCheck.rectTransform, DetW - 111f, 46f, 36f, 36f);
        dDone = Lbl(detRt, "Получено", 20f, Dim, TextAlignmentOptions.Center, true, DetW - 196f, 70f, 170f, 28f);

        detRt.gameObject.SetActive(false);
    }

    // ============================================================ кнопка в HUD
    void BuildHud()
    {
        var cgo = new GameObject("AchievementsHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;                               // выше HUD (10), ниже окон (30+)
        ConfigScaler(cgo.GetComponent<CanvasScaler>());
        hudGroup = cgo.GetComponent<CanvasGroup>();
        hudGroup.alpha = 0f;
        hudGroup.blocksRaycasts = false;

        var btn = UiKit.MakeButton(cgo.transform, "", UiBtn.Yellow, new Vector2(HudBtn, HudBtn), 20f, 1f);
        btn.name = "AchievementsButton";
        hudRt = btn.GetComponent<RectTransform>();
        // справа налево: «Прокачка», питомцы, достижения
        float x = -(UpgradeWindow.HudRight + UpgradeWindow.HudBtnW + UpgradeWindow.HudGap + HudBtn + UpgradeWindow.HudGap);
        Anch(hudRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(x, UpgradeWindow.HudBottom), new Vector2(HudBtn, HudBtn));
        btn.onClick.AddListener(Toggle);

        hudIcon = UiKit.MakeIcon(btn.transform, UiIcon.Trophy, 58f, "Cup");
        hudIcon.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        if (!Application.isMobilePlatform)
        {
            var key = UiKit.MakeKeycap(btn.transform, "K", 38f);
            Anch(key.rectTransform, new Vector2(0f, 1f), Ctr, new Vector2(8f, -8f), new Vector2(38f, 38f));
        }

        // красная точка с числом: награды ждут
        var dot = UiKit.MakeImage(btn.transform, "Dot", UiKit.Medallion, false);
        dot.color = DotColor;
        hudDot = dot.rectTransform;
        Anch(hudDot, new Vector2(1f, 1f), Ctr, new Vector2(-8f, -8f), new Vector2(34f, 34f));
        dotLabel = UiKit.MakeLabel(dot.transform, "!", 22f, Color.white, TextAlignmentOptions.Center, true);
        Stretch(dotLabel.rectTransform);
        hudDot.gameObject.SetActive(false);
    }

    void UpdateHud(float dt)
    {
        if (hudGroup == null) return;
        if (!hudRegistered && HudSafeAreaRegistry.Instance != null)
        {
            HudSafeAreaRegistry.Instance.Register(hudRt);
            hudRegistered = true;
        }

        bool visible = Available && !MobileInput.ModalOpen && !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, visible ? 1f : 0f, dt / 0.2f);
        bool on = visible && hudGroup.alpha > 0.5f;
        hudGroup.blocksRaycasts = on;
        hudGroup.interactable = on;

        var m = AchievementManager.Instance;
        int n = visible && m != null ? m.ClaimableCount : 0;
        bool hint = n > 0;
        if (hudDot.gameObject.activeSelf != hint) hudDot.gameObject.SetActive(hint);
        if (hint)
        {
            Set(dotLabel, n > 9 ? "9+" : n.ToString());
            dotT += dt;
            hudDot.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(dotT * 5f));
            // кубок встряхивается раз в пару секунд: «забери меня»
            float kick = Mathf.Clamp01(Mathf.Sin(dotT * 1.6f) * 3f - 1.5f);
            hudIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(dotT * 18f) * 10f * kick);
        }
        else if (hudIcon != null) hudIcon.rectTransform.localEulerAngles = Vector3.zero;
    }

    // ============================================================ мелкие помощники
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void AddBtn(Image img, UnityEngine.Events.UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(a);
    }
}

// наведение и клик по медальону узла (клик не срабатывает, если игрок тянул дерево)
public class AchNodeHit : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public System.Action onEnter, onExit, onClick;
    public void OnPointerEnter(PointerEventData e) { if (onEnter != null) onEnter(); }
    public void OnPointerExit(PointerEventData e) { if (onExit != null) onExit(); }
    public void OnPointerClick(PointerEventData e) { if (onClick != null) onClick(); }
}
