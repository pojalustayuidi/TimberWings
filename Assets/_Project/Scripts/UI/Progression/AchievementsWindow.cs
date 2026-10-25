// Assets/_Project/Scripts/UI/Progression/AchievementsWindow.cs (НЕ в Editor)
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

// ----- КАРТА ФАЙЛОВ (класс разбит на части partial, поведение не менялось) -----
// AchievementsWindow.cs: статический API, размеры, цвета, состояние, жизненный цикл (этот файл)
// AchievementsWindow.Anim.cs: камера, фон с параллаксом, анимация узлов, огоньки, карточка выбранного
// AchievementsWindow.TabsData.cs: вкладки, данные -> окно, действия
// AchievementsWindow.Build.cs: сборка окна, раскладка вкладки, карточка выбранного
// AchievementsWindow.HudHelpers.cs: кнопка кубка в HUD и мелкие помощники
public partial class AchievementsWindow : MonoBehaviour
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
        UiInputGuard.EnsureEventSystem();
        Build();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterModal() { UiModal.Register(typeof(AchievementsWindow), () => IsOpen, Close); }

    static bool OtherWindowOpen()
    {
        return UiModal.AnyOpen(typeof(AchievementsWindow));   // реестр окон: UiModal
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

}

// наведение и клик по медальону узла (клик не срабатывает, если игрок тянул дерево)
public class AchNodeHit : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public System.Action onEnter, onExit, onClick;
    public void OnPointerEnter(PointerEventData e) { if (onEnter != null) onEnter(); }
    public void OnPointerExit(PointerEventData e) { if (onExit != null) onExit(); }
    public void OnPointerClick(PointerEventData e) { if (onClick != null) onClick(); }
}
