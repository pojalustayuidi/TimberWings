// Assets/_Project/Scripts/UI/Progression/SkillTreeUi.cs (НЕ в Editor)
// Правая панель окна прокачки (UpgradeWindow): дерево навыков. Всё строится из кода, настраивать в сцене ничего не нужно.
// Окно создаёт этот компонент само (UpgradeWindow.Build) и даёт ему панель 682 x 807. Стиль: Candy (внутри UiKit.UseSkin).
// Панель светлая (лавандовая), поэтому весь текст индиго или приглушённый индиго: белого и бледно-жёлтого нет (подписи узлов окрашены по состоянию).
//   Вверху: «Очков: N», кнопка «Сбросить навыки» и под ней цена сброса чипом с монетой (с подтверждением).
//   Три ветки (Лесоруб, Носильщик, Хозяин), три яруса: ярус I один узел, ярусы II и III два узла-альтернативы.
//   Каждый узел это вымпел UiKit.Banner. Состояние читается по цвету и значку, без текста:
//       изучено          мятный вымпел, галочка в углу, мягкое свечение
//       можно изучить    лимонный вымпел, пульсирует и покачивается (CandyIdleMotion), свечение дышит, иногда блестит
//       не хватает очков бледно-лимонный, не пульсирует
//       закрыто          лавандовый вымпел с замком вместо значка
//       занят соседом    выцветший лавандовый, значок бледный, красный крестик в углу
//   Линии связей: мягкие кривые с круглыми концами (SkillLinkGraphic). Путь к изученному узлу мятный, к доступному лимонный.
//   Тап по узлу выбирает его (белое кольцо), карточка внизу окрашена в цвет состояния, цена чипом, кнопка «Изучить» покупает.
//   Покупка: искры и кольцо CandyFx из центра вымпела, вымпел «подпрыгивает».
// Логика только в PlayerSkills (CanBuy / TryBuy / TryReset / GetState), окно лишь показывает и вызывает её.
// Подписка на PlayerSkills.Changed в OnEnable/OnDisable. MobileInput.ModalOpen держит само окно прокачки (UpgradeWindow.IsOpen).
// Искры работают, если нашёлся CandyFxKit (UiCandy.Kit ищет его сам). Без него всё остальное работает, только без искр.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

// мягкая линия по точкам: одним мешем, закруглённые концы, размытый край. Точки в координатах RectTransform (начало: левый верхний угол)
public sealed class SkillLinkGraphic : MaskableGraphic
{
    public readonly List<Vector2> points = new List<Vector2>(24);
    public float thickness = 6f;
    public float feather = 1.6f;      // ширина мягкого края, px

    public void SetThickness(float v) { if (Mathf.Abs(v - thickness) > 0.01f) { thickness = v; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int n = points.Count;
        if (n < 2) return;

        float h = thickness * 0.5f, f = Mathf.Max(0.01f, feather);
        Color c = color, c0 = color;
        c0.a = 0f;

        // по четыре вершины на точку: мягкий край, ядро, ядро, мягкий край (одна лента без перекрытий, поэтому прозрачность не «пятнает»)
        for (int i = 0; i < n; i++)
        {
            Vector2 a = points[Mathf.Max(0, i - 1)], b = points[Mathf.Min(n - 1, i + 1)];
            Vector2 t = b - a;
            if (t.sqrMagnitude < 1e-6f) t = Vector2.down;
            t.Normalize();
            Vector2 nr = new Vector2(-t.y, t.x);
            Vector2 p = points[i];
            vh.AddVert(p + nr * (h + f), c0, Vector2.zero);
            vh.AddVert(p + nr * h, c, Vector2.zero);
            vh.AddVert(p - nr * h, c, Vector2.zero);
            vh.AddVert(p - nr * (h + f), c0, Vector2.zero);
        }
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 4, b = (i + 1) * 4;
            for (int k = 0; k < 3; k++)
            {
                vh.AddTriangle(a + k, a + k + 1, b + k + 1);
                vh.AddTriangle(a + k, b + k + 1, b + k);
            }
        }

        Cap(vh, points[0], points[0] - points[1], h, f, c, c0);
        Cap(vh, points[n - 1], points[n - 1] - points[n - 2], h, f, c, c0);
    }

    // круглый конец: полукруг наружу от конца линии, по краю мягкое кольцо
    static void Cap(VertexHelper vh, Vector2 p, Vector2 outward, float h, float f, Color c, Color c0)
    {
        if (outward.sqrMagnitude < 1e-6f) outward = Vector2.up;
        outward.Normalize();
        const int S = 8;
        float a0 = Mathf.Atan2(outward.y, outward.x) - Mathf.PI * 0.5f;
        int baseIdx = vh.currentVertCount;
        vh.AddVert(p, c, Vector2.zero);
        for (int k = 0; k <= S; k++)
        {
            float ang = a0 + Mathf.PI * k / S;
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            vh.AddVert(p + d * h, c, Vector2.zero);
            vh.AddVert(p + d * (h + f), c0, Vector2.zero);
        }
        for (int k = 0; k < S; k++)
        {
            int in0 = baseIdx + 1 + k * 2, out0 = in0 + 1, in1 = in0 + 2, out1 = in0 + 3;
            vh.AddTriangle(baseIdx, in0, in1);
            vh.AddTriangle(in0, out0, out1);
            vh.AddTriangle(in0, out1, in1);
        }
    }
}

public sealed class SkillTreeUi : MonoBehaviour
{
    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080, область 1322 x 807)
    const float W = 682f, H = 807f;
    const float PennW = 66f, PennH = 84f, NodeOff = 48f;           // вымпел (пропорции Banner 50:64) и сдвиг левого и правого узла от оси ветки
    const float LabelW1 = 200f, LabelW2 = 84f, LabelH = 38f;       // ширина подписи узла (ярус I / ярусы II и III) и её высота (две строки)
    const float LineT = 6f;                                        // толщина линии связи
    static readonly float[] TierY = { 205f, 356f, 510f };          // центры ярусов
    static readonly float[] ColX = { 114f, 341f, 568f };           // оси веток
    const float CardY = 606f, CardH = 198f;

    static readonly Color Soft = UiCandy.Muted;                                                   // второстепенный текст
    static readonly Color Dim = new Color(UiCandy.Muted.r, UiCandy.Muted.g, UiCandy.Muted.b, 0.8f); // подписи закрытых узлов
    static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

    // цвета веток не повторяют цвета состояний (мята, лимон, лаванда), чтобы заголовок не путался с узлом
    static readonly Color[] BranchColors =
    {
        new Color32(0xFF, 0x9A, 0x7A, 255), new Color32(0x73, 0xBD, 0xFF, 255), new Color32(0xFF, 0x8F, 0xBE, 255)
    };
    static readonly UiIcon[] BranchIcons = { UiIcon.Log, UiIcon.Boot, UiIcon.Coin };
    static readonly string[] TierNames = { "I", "II", "III" };

    // иконка каждого узла (набор UiIcon ограничен, подобрано по смыслу)
    static UiIcon IconFor(string id)
    {
        switch (id)
        {
            case SkillIds.LumberChain: return UiIcon.Axe;
            case SkillIds.LumberSturdy: return UiIcon.Log;
            case SkillIds.LumberFastGrow: return UiIcon.ArrowUp;
            case SkillIds.LumberEcho: return UiIcon.Target;
            case SkillIds.LumberGolden: return UiIcon.Star;
            case SkillIds.CarryLight: return UiIcon.Boot;
            case SkillIds.CarryDash: return UiIcon.ArrowRight;
            case SkillIds.CarryBasket: return UiIcon.Backpack;
            case SkillIds.CarryFriend: return UiIcon.Paw;
            case SkillIds.CarryLegs: return UiIcon.Clock;
            case SkillIds.BossTongue: return UiIcon.Coin;
            case SkillIds.BossHoard: return UiIcon.Box;
            case SkillIds.BossChief: return UiIcon.Tent;
            case SkillIds.BossBulk: return UiIcon.Gem;
            case SkillIds.BossSawyer: return UiIcon.Saw;
            default: return UiIcon.Star;
        }
    }

    // ------------------------------------------------------------ цвета состояний
    static readonly Color Grey = new Color(0.62f, 0.62f, 0.72f, 1f);

    static Color PennantColor(SkillNodeState s)
    {
        switch (s)
        {
            case SkillNodeState.Bought: return UiCandy.Mint;
            case SkillNodeState.Available: return UiCandy.Lemon;
            case SkillNodeState.NoPoints: return Color.Lerp(UiCandy.Lemon, Grey, 0.5f);          // лимон, выцветший: «ещё не сейчас»
            case SkillNodeState.Locked: return UiCandy.Lavender;
            default: return Color.Lerp(UiCandy.Lavender, Grey, 0.55f);                           // Blocked: ещё бледнее лаванды
        }
    }

    static UiTone ToneOf(SkillNodeState s)
    {
        switch (s)
        {
            case SkillNodeState.Bought: return UiTone.Mint;
            case SkillNodeState.Available: return UiTone.Lemon;
            case SkillNodeState.NoPoints: return UiTone.Peach;
            case SkillNodeState.Locked: return UiTone.Lavender;
            default: return UiTone.Locked;
        }
    }

    // ------------------------------------------------------------ мелкие спрайты (рисуются один раз)
    static Sprite pillSp, glowSp, ringSp;

    static Texture2D NewTex(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
    }

    // скруглённый прямоугольник с полным скруглением: растягивается как пилюля (9-slice), квадратный даёт круг
    static Sprite Pill
    {
        get
        {
            if (pillSp != null) return pillSp;
            const int n = 34;
            const float r = 16f;
            var tex = NewTex(n, n);
            var cols = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - n * 0.5f) - (n * 0.5f - r);
                    float py = Mathf.Abs(y + 0.5f - n * 0.5f) - (n * 0.5f - r);
                    float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude - r;
                    cols[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(cols);
            tex.Apply();
            pillSp = Sprite.Create(tex, new Rect(0, 0, n, n), Mid, 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
            pillSp.name = "SkillTreeUi_Pill";
            return pillSp;
        }
    }

    // мягкое круглое свечение
    static Sprite GlowSprite
    {
        get
        {
            if (glowSp != null) return glowSp;
            const int n = 64;
            var tex = NewTex(n, n);
            var cols = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    cols[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(cols);
            tex.Apply();
            glowSp = Sprite.Create(tex, new Rect(0, 0, n, n), Mid, 100f);
            glowSp.name = "SkillTreeUi_Glow";
            return glowSp;
        }
    }

    // тонкое кольцо выбора
    static Sprite RingSprite
    {
        get
        {
            if (ringSp != null) return ringSp;
            const int n = 128;
            var tex = NewTex(n, n);
            var cols = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    float band = Mathf.Abs(d - 58f) - 3f;
                    cols[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - band));
                }
            tex.SetPixels(cols);
            tex.Apply();
            ringSp = Sprite.Create(tex, new Rect(0, 0, n, n), Mid, 100f);
            ringSp.name = "SkillTreeUi_Ring";
            return ringSp;
        }
    }

    // значки берём в скине Candy, даже если окно сейчас вне using (иначе вернётся латунный значок)
    static Sprite CandyIcon(UiIcon icon)
    {
        using (UiKit.UseSkin(UiSkin.Candy)) return UiKit.Icon(icon);
    }

    // ------------------------------------------------------------ данные
    class Node
    {
        public SkillNodeDef def;
        public RectTransform rt;        // держатель: клик, наведение и «подпрыгивание» масштабом
        public RectTransform body;      // вымпел со значком: на нём покачивание CandyIdleMotion (отдельно от масштаба держателя)
        public Image glow, ring, pennant, icon, lockImg;
        public GameObject checkBadge, crossBadge;
        public CandyIdleMotion idle;
        public TMP_Text label;
        public SkillNodeState state, shown;
        public bool known;
        public float hover = 1f;
        public CandySpring pop = new CandySpring(0f);
    }

    class Link { public SkillLinkGraphic g; public string needId; }   // needId: узел, по состоянию которого красится линия

    public RectTransform Root { get; private set; }
    public bool ConfirmOpen { get { return confirmRoot != null && confirmRoot.activeSelf; } }

    bool built;
    string selId;
    string flashId;
    float flashUntil, nextTwinkle;
    readonly List<Node> nodes = new List<Node>();
    readonly List<Link> links = new List<Link>();
    readonly Dictionary<string, SkillNodeState> states = new Dictionary<string, SkillNodeState>();

    TMP_Text pointsText, nextText, resetBtnTxt;
    UiCandy.ChipRefs resetChip;
    Button resetBtn;

    Image tip, cardPennant, tipIcon;
    TMP_Text tipTitle, tipSub, tipDesc, tipStatus, tipBtnTxt;
    UiCandy.ChipRefs tipCost;
    Button tipBtn;

    GameObject confirmRoot;
    TMP_Text confirmText;
    UiCandy.ChipRefs confirmChip;
    Button confirmYes;

    // ------------------------------------------------------------ жизненный цикл
    void OnEnable() { PlayerSkills.Changed += OnChanged; }
    void OnDisable() { PlayerSkills.Changed -= OnChanged; }

    void OnChanged() { if (built) Refresh(); }

    // true, если подтверждение было открыто и закрылось (Esc: сначала закрывается оно, потом окно)
    public bool CloseConfirm()
    {
        if (!ConfirmOpen) return false;
        confirmRoot.SetActive(false);
        return true;
    }

    void Update()
    {
        if (!built || !Root.gameObject.activeInHierarchy) return;

        float t = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.2f);
        bool hoverFx = !Application.isMobilePlatform && !ConfirmOpen;
        Vector2 mouse = Input.mousePosition;

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            Color gc;
            if (n.def.Id == flashId && t < flashUntil) gc = new Color(1f, 0.97f, 0.75f, 0.95f);
            else if (n.state == SkillNodeState.Bought) gc = new Color(UiCandy.Mint.r, UiCandy.Mint.g, UiCandy.Mint.b, 0.40f);
            else if (n.state == SkillNodeState.Available) gc = new Color(UiCandy.Lemon.r, UiCandy.Lemon.g, UiCandy.Lemon.b, 0.30f + 0.32f * pulse);
            else if (n.state == SkillNodeState.NoPoints) gc = new Color(UiCandy.Lemon.r, UiCandy.Lemon.g, UiCandy.Lemon.b, 0.10f);
            else gc = Clear;
            n.glow.color = gc;
            n.ring.color = n.def.Id == selId ? new Color(1f, 1f, 1f, 0.55f + 0.35f * pulse) : Clear;

            float target = hoverFx && RectTransformUtility.RectangleContainsScreenPoint(n.rt, mouse, null) ? 1.08f : 1f;
            n.hover = Mathf.MoveTowards(n.hover, target, dt * 1.2f);
            n.pop.Step(0f, 260f, 9f, dt);
            n.rt.localScale = Vector3.one * (n.hover * (1f + n.pop.x * 0.2f));
        }

        // изредка блестит один из доступных вымпелов
        if (t >= nextTwinkle)
        {
            nextTwinkle = t + Random.Range(1.1f, 2.2f);
            Node pick = null;
            int count = 0;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].state == SkillNodeState.Available && Random.Range(0, ++count) == 0) pick = nodes[i];
            if (pick != null) CandyFx.Twinkle(UiCandy.Kit, pick.body, new Color(1f, 0.96f, 0.65f, 1f));
        }
    }

    // ------------------------------------------------------------ действия
    Node NodeOf(string id)
    {
        for (int i = 0; i < nodes.Count; i++) if (nodes[i].def.Id == id) return nodes[i];
        return null;
    }

    void Select(string id)
    {
        selId = id;
        var n = NodeOf(id);
        if (n != null) n.pop.v += 4f;
        Refresh();
    }

    void BuySelected()
    {
        if (string.IsNullOrEmpty(selId)) return;
        var n = NodeOf(selId);
        if (PlayerSkills.TryBuy(selId))
        {
            flashId = selId;
            flashUntil = Time.unscaledTime + 0.6f;
            if (n != null)
            {
                n.pop.v += 8f;
                var kit = UiCandy.Kit;
                CandyFx.Burst(kit, n.rt, new Color(1f, 0.92f, 0.55f, 1f), 14, 300f);
                CandyFx.Burst(kit, n.rt, UiCandy.Mint, 8, 210f);
                CandyFx.Ring(kit, n.rt, new Color(1f, 1f, 1f, 0.9f));
            }
        }
        Refresh();
    }

    void OpenConfirm()
    {
        string reason;
        if (!PlayerSkills.CanReset(out reason)) return;
        int cost = PlayerSkills.ResetCost;
        if (cost > 0)
        {
            confirmText.text = "Все навыки будут сняты, очки вернутся.\nСброс стоит:";
            SetChip(confirmChip, cost.ToString(), UiCandy.GoldText, UiTone.Peach, UiIcon.Coin);
        }
        else
        {
            confirmText.text = "Все навыки будут сняты, очки вернутся.\nПервый сброс <color=#2A9573>бесплатный</color>, дальше он стоит "
                               + GameBalance.SkillResetCoins + " монет.";
        }
        confirmChip.bg.gameObject.SetActive(cost > 0);
        confirmRoot.SetActive(true);
    }

    void ConfirmReset()
    {
        confirmRoot.SetActive(false);
        PlayerSkills.TryReset();
        Refresh();
    }

    // ------------------------------------------------------------ обновление
    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    // чип: текст, цвет текста, оттенок фона, значок (значок берётся в Candy)
    static void SetChip(UiCandy.ChipRefs c, string text, Color col, UiTone tone, UiIcon icon)
    {
        Set(c.label, text);
        c.label.color = col;
        c.bg.color = UiCandy.TintOf(tone);
        var sp = CandyIcon(icon);
        if (c.icon.sprite != sp) c.icon.sprite = sp;
    }

    public void Refresh()
    {
        if (!built) return;

        int free = PlayerSkills.FreePoints, total = PlayerSkills.TotalPoints;
        Set(pointsText, "Очков: <color=#" + (free > 0 ? "2A9573" : "5E6A96") + ">" + free + "</color>");   // есть очки: зелёный, нет: приглушённый индиго
        int level = PlayerStats.Level;
        Set(nextText, total >= GameBalance.SkillMaxPoints ? "Все очки получены (" + total + " из " + GameBalance.SkillMaxPoints + ")"
                                                         : "Всего " + total + " из " + GameBalance.SkillMaxPoints + ", следующее на " + (level + 1) + "-м уровне");

        // кнопка сброса и цена чипом с монетой
        string rr;
        bool canReset = PlayerSkills.CanReset(out rr);
        int cost = PlayerSkills.ResetCost;
        if (cost > 0) SetChip(resetChip, cost.ToString(), UiCandy.GoldText, UiTone.Peach, UiIcon.Coin);
        else SetChip(resetChip, "бесплатно", UiCandy.Done, UiTone.Mint, UiIcon.Check);
        var chipBg = resetChip.bg.color;
        chipBg.a = canReset ? 1f : 0.55f;
        resetChip.bg.color = chipBg;
        resetBtn.interactable = canReset;
        resetBtnTxt.color = canReset ? UiCandy.Ink : UiCandy.Muted;

        // узлы и линии
        states.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            n.state = PlayerSkills.GetState(n.def.Id);
            states[n.def.Id] = n.state;
            ApplyLook(n);
        }
        for (int i = 0; i < links.Count; i++)
        {
            var l = links[i];
            SkillNodeState s;
            bool have = l.needId != null && states.TryGetValue(l.needId, out s);
            s = have ? states[l.needId] : SkillNodeState.Locked;
            Color lc;
            switch (s)
            {
                case SkillNodeState.Bought: lc = UiCandy.Mint; break;
                case SkillNodeState.Available: lc = new Color(UiCandy.Lemon.r, UiCandy.Lemon.g, UiCandy.Lemon.b, 0.85f); break;
                case SkillNodeState.NoPoints: lc = new Color(UiCandy.Lemon.r, UiCandy.Lemon.g, UiCandy.Lemon.b, 0.60f); break;
                default: lc = new Color(UiCandy.LavenderDeep.r, UiCandy.LavenderDeep.g, UiCandy.LavenderDeep.b, 0.50f); break;
            }
            l.g.color = lc;
            l.g.SetThickness(s == SkillNodeState.Bought ? LineT + 1f : LineT - 1f);
        }

        RefreshCard();
    }

    void ApplyLook(Node n)
    {
        var st = n.state;
        bool closed = st == SkillNodeState.Locked || st == SkillNodeState.Blocked;

        n.pennant.color = PennantColor(st);
        n.icon.gameObject.SetActive(st != SkillNodeState.Locked);
        n.icon.color = st == SkillNodeState.Blocked ? new Color(1f, 1f, 1f, 0.35f)
                     : st == SkillNodeState.NoPoints ? new Color(1f, 1f, 1f, 0.8f) : Color.white;
        n.lockImg.gameObject.SetActive(st == SkillNodeState.Locked);
        n.crossBadge.SetActive(st == SkillNodeState.Blocked);
        n.checkBadge.SetActive(st == SkillNodeState.Bought);
        n.label.color = st == SkillNodeState.Bought ? UiCandy.Done          // изучено: зелёный
                      : st == SkillNodeState.Available ? UiCandy.Ink        // можно изучить: основной индиго
                      : st == SkillNodeState.NoPoints ? UiCandy.Muted       // не хватает очков: приглушённый
                      : Dim;                                                // закрыто или занято: ещё бледнее

        // покачивание и размер меняем только при смене состояния: CandyIdleMotion сам добавляет и убирает свою добавку
        if (n.known && n.shown == st) return;
        n.known = true;
        n.shown = st;
        n.idle.enabled = false;                                   // OnDisable возвращает позу
        n.body.localScale = Vector3.one * (closed ? 0.92f : 1f);  // закрытые чуть меньше: ещё одна подсказка формой
        n.body.localRotation = Quaternion.identity;
        n.body.anchoredPosition = Vector2.zero;
        if (st == SkillNodeState.Available) n.idle.enabled = true;
    }

    void RefreshCard()
    {
        var def = SkillTree.Get(selId);
        if (def == null) return;

        var st = PlayerSkills.GetState(def.Id);
        bool closed = st == SkillNodeState.Locked || st == SkillNodeState.Blocked;
        tip.color = UiCandy.TintOf(ToneOf(st));
        cardPennant.color = PennantColor(st);
        tipIcon.sprite = CandyIcon(IconFor(def.Id));
        tipIcon.color = closed ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        Set(tipTitle, def.Title);
        Set(tipSub, SkillTree.BranchName(def.Branch) + ", ярус " + TierNames[def.Tier - 1] + " · с " + def.RequiredLevel + "-го уровня");
        Set(tipDesc, def.Description);

        string reason;
        bool can = PlayerSkills.CanBuy(def.Id, out reason);
        string status;
        switch (st)
        {
            case SkillNodeState.Bought: status = "<color=#2A9573>Изучено</color>"; break;
            case SkillNodeState.Available: status = "<color=#2A9573>Можно изучить</color>"; break;
            case SkillNodeState.NoPoints: status = "<color=#C8365C>" + reason + "</color>"; break;
            case SkillNodeState.Blocked: status = "<color=#C8365C>" + reason + "</color>"; break;
            default: status = reason; break;   // Locked: «Нужен 4 уровень» или «Сначала изучите …»
        }
        Set(tipStatus, status);

        bool bought = st == SkillNodeState.Bought;
        tipCost.bg.gameObject.SetActive(!bought);
        bool afford = PlayerSkills.FreePoints >= def.Cost;
        SetChip(tipCost, def.Cost + " " + UiDraw.Plural(def.Cost, "очко", "очка", "очков"),
                afford ? UiCandy.GoldText : UiCandy.Danger, afford ? UiTone.Neutral : UiTone.Danger, UiIcon.Star);

        Set(tipBtnTxt, bought ? "Изучено" : "Изучить");
        tipBtnTxt.color = can ? UiCandy.Ink : UiCandy.Muted;
        tipBtn.interactable = can;
    }

    // ------------------------------------------------------------ сборка UI
    // parent: правая панель окна (w x h = 682 x 807). Возвращает корень дерева.
    public RectTransform Build(Transform parent, float w, float h)
    {
        if (built) return Root;

        using (UiKit.UseSkin(UiSkin.Candy)) BuildAll(parent);

        selId = SkillTree.All.Count > 0 ? SkillTree.All[0].Id : null;
        built = true;
        Refresh();
        return Root;
    }

    void BuildAll(Transform parent)
    {
        Root = NewRT("SkillsTree", parent);
        Stretch(Root);

        BuildHeader();
        for (int b = 0; b < 3; b++) BuildBranchHeader(b);

        // линии рисуем до узлов, чтобы вымпелы лежали сверху
        for (int b = 0; b < 3; b++) BuildLines(b);

        // узлы: ярус I по центру, ярусы II и III: первый узел группы слева, второй справа
        var all = SkillTree.All;
        var seen = new Dictionary<int, int>();
        for (int i = 0; i < all.Count; i++)
        {
            var d = all[i];
            int b = (int)d.Branch;
            float cx = ColX[b], cy = TierY[d.Tier - 1];
            if (d.Tier > 1)
            {
                int key = b * 10 + d.Tier, k;
                seen.TryGetValue(key, out k);
                seen[key] = k + 1;
                cx += k == 0 ? -NodeOff : NodeOff;
            }
            nodes.Add(BuildNode(d, cx, cy));
        }

        BuildCard();
        BuildConfirm();
    }

    void BuildHeader()
    {
        var star = UiKit.MakeIcon(Root, UiIcon.Star, 36f, "PointsIcon");
        TLC(star.rectTransform, 34f, 28f, 36f, 36f);
        pointsText = UiKit.MakeLabel(Root, "", 28f, UiCandy.Ink, TextAlignmentOptions.Left, true);
        TL(pointsText.rectTransform, 60f, 8f, 250f, 40f);
        AutoSize(pointsText, 16f, 28f);
        nextText = UiKit.MakeLabel(Root, "", 17f, Soft, TextAlignmentOptions.Left, false);
        TL(nextText.rectTransform, 16f, 54f, 350f, 28f);
        AutoSize(nextText, 11f, 17f);

        // сброс справа: кнопка, под ней цена чипом с монетой
        resetBtn = UiKit.MakeButton(Root, "Сбросить навыки", UiBtn.Amber, new Vector2(240f, 52f), 22f, 1f);
        TR(resetBtn.GetComponent<RectTransform>(), 16f, 8f, 240f, 52f);
        resetBtnTxt = resetBtn.GetComponentInChildren<TMP_Text>();
        AutoSize(resetBtnTxt, 13f, 22f);
        resetBtn.onClick.AddListener(OpenConfirm);

        resetChip = UiCandy.Chip(Root, UiIcon.Coin, "0", 34f, UiTone.Peach);
        Anch(resetChip.rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -66f), new Vector2(0f, 34f));
    }

    // заголовок ветки: конфетная пилюля с контуром, значок и название
    void BuildBranchHeader(int b)
    {
        float cx = ColX[b];

        var outline = UiKit.MakeImage(Root, "StripOutline" + b, Pill, true, 1f);
        outline.color = UiCandy.Ink;
        TLC(outline.rectTransform, cx, 120f, 216f, 44f);

        var strip = UiKit.MakeImage(outline.transform, "Strip" + b, Pill, true, 1f);
        strip.color = BranchColors[b];
        Anch(strip.rectTransform, Mid, Mid, Vector2.zero, new Vector2(211f, 39f));

        var shine = UiKit.MakeImage(strip.transform, "Shine", Pill, true, 0.4f);
        shine.color = new Color(1f, 1f, 1f, 0.45f);
        Anch(shine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(170f, 6f));

        var ic = UiKit.MakeIcon(strip.transform, BranchIcons[b], 28f, "BranchIcon");
        Anch(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24f, -1f), new Vector2(28f, 28f));
        var nm = UiKit.MakeLabel(strip.transform, SkillTree.BranchName((SkillBranch)b).ToUpperInvariant(), 20f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        nm.rectTransform.offsetMin = new Vector2(42f, 0f);
        nm.rectTransform.offsetMax = new Vector2(-10f, -2f);
        AutoSize(nm, 11f, 20f);
    }

    // точка в координатах линии: начало в левом верхнем углу панели, y вверх, поэтому минус
    static Vector2 P(float x, float y) { return new Vector2(x, -y); }

    void BuildLines(int b)
    {
        float cx = ColX[b];
        var root = SkillTree.Tier1Of((SkillBranch)b);
        string rootId = root != null ? root.Id : null;
        float half = PennH * 0.5f;

        float label1Bottom = TierY[0] + half + 4f + LabelH;     // низ подписи узла I: отсюда ствол
        float label2Bottom = TierY[1] + half + 4f + LabelH;     // низ подписей яруса II: отсюда развилка яруса III

        // короткий отрезок от заголовка до вымпела I и ствол между вымпелами яруса II
        AddLink(rootId, new List<Vector2> { P(cx, 142f), P(cx, TierY[0] - half + 3f) });
        AddLink(rootId, new List<Vector2> { P(cx, label1Bottom), P(cx, label2Bottom) });

        // развилки: мягкие S-образные кривые от ствола к вымпелам. Кривая загорается, когда изучен сам узел, куда она ведёт
        var all = SkillTree.All;
        for (int tier = 2; tier <= 3; tier++)
        {
            float y0 = tier == 2 ? label1Bottom : label2Bottom;
            float y1 = TierY[tier - 1] - half + 2f;
            int k = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if ((int)all[i].Branch != b || all[i].Tier != tier) continue;
                float nx = cx + (k == 0 ? -NodeOff : NodeOff);
                AddLink(all[i].Id, Fork(cx, y0, nx, y1));
                k++;
            }
        }
    }

    // кубическая кривая Безье с вертикальными касательными на концах: мягкий «Y»
    static List<Vector2> Fork(float x0, float y0, float x1, float y1)
    {
        const int S = 14;
        float dy = Mathf.Max(8f, (y1 - y0) * 0.5f);
        Vector2 a = P(x0, y0), b = P(x0, y0 + dy), c = P(x1, y1 - dy), d = P(x1, y1);
        var pts = new List<Vector2>(S + 1);
        for (int i = 0; i <= S; i++)
        {
            float t = i / (float)S, u = 1f - t;
            pts.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
        }
        return pts;
    }

    void AddLink(string needId, List<Vector2> pts)
    {
        var rt = NewRT("Link", Root);
        Anch(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(W, H));
        var g = rt.gameObject.AddComponent<SkillLinkGraphic>();
        g.raycastTarget = false;
        g.thickness = LineT;
        g.points.AddRange(pts);
        g.color = new Color(UiCandy.LavenderDeep.r, UiCandy.LavenderDeep.g, UiCandy.LavenderDeep.b, 0.50f);
        links.Add(new Link { g = g, needId = needId });
    }

    // значок-бейдж в углу вымпела: белый кружок с контуром и значок внутри
    GameObject Badge(Transform parent, UiIcon icon, string name)
    {
        var ring = UiKit.MakeImage(parent, name, Pill, false);
        ring.color = UiCandy.Ink;
        Anch(ring.rectTransform, new Vector2(1f, 1f), Mid, new Vector2(-3f, -8f), new Vector2(30f, 30f));
        var fill = UiKit.MakeImage(ring.transform, "Fill", Pill, false);
        fill.color = Color.white;
        Anch(fill.rectTransform, Mid, Mid, Vector2.zero, new Vector2(25f, 25f));
        var ic = UiKit.MakeIcon(fill.transform, icon, 19f);
        ic.rectTransform.anchoredPosition = Vector2.zero;
        return ring.gameObject;
    }

    Node BuildNode(SkillNodeDef d, float cx, float cy)
    {
        var n = new Node { def = d };

        // держатель: невидимая картинка под клик, внутри свечение, кольцо выбора и вымпел
        var holder = NewRT("Node_" + d.Id, Root);
        TLC(holder, cx, cy, PennW, PennH);
        var hit = holder.gameObject.AddComponent<Image>();
        hit.color = Clear;
        string id = d.Id;
        AddClick(hit, () => Select(id));
        n.rt = holder;

        n.glow = UiKit.MakeImage(holder, "Glow", GlowSprite, false);
        n.glow.color = Clear;
        Anch(n.glow.rectTransform, Mid, Mid, Vector2.zero, new Vector2(PennW + 66f, PennH + 66f));

        n.ring = UiKit.MakeImage(holder, "SelectRing", RingSprite, false);
        n.ring.color = Clear;
        Anch(n.ring.rectTransform, Mid, Mid, new Vector2(0f, 2f), new Vector2(PennH + 30f, PennH + 30f));

        n.body = NewRT("Body", holder);
        Anch(n.body, Mid, Mid, Vector2.zero, new Vector2(PennW, PennH));

        n.pennant = UiKit.MakeImage(n.body, "Pennant", UiKit.Banner, false);
        n.pennant.preserveAspect = true;
        Stretch(n.pennant.rectTransform);

        n.icon = UiKit.MakeIcon(n.body, IconFor(d.Id), 38f);
        n.icon.rectTransform.anchoredPosition = new Vector2(0f, 3f);
        n.lockImg = UiKit.MakeIcon(n.body, UiIcon.Lock, 34f, "Lock");
        n.lockImg.rectTransform.anchoredPosition = new Vector2(0f, 3f);

        n.checkBadge = Badge(holder, UiIcon.Check, "CheckBadge");
        n.crossBadge = Badge(holder, UiIcon.Cross, "BlockedBadge");

        // «дыхание» доступного вымпела: включается только в состоянии Available (см. ApplyLook)
        n.idle = n.body.gameObject.AddComponent<CandyIdleMotion>();
        n.idle.bobHeight = 2.5f;
        n.idle.bobSpeed = 2.4f;
        n.idle.wobbleAngle = 2.2f;
        n.idle.wobbleSpeed = 2f;
        n.idle.pulse = 0.05f;
        n.idle.pulseSpeed = 3.2f;
        n.idle.randomPhase = true;
        n.idle.enabled = false;

        // название под вымпелом: до двух строк (колонки узкие)
        n.label = UiKit.MakeLabel(Root, d.Title, 16f, UiCandy.Ink, TextAlignmentOptions.Top, true);
        TLC(n.label.rectTransform, cx, cy + PennH * 0.5f + 4f + LabelH * 0.5f, d.Tier == 1 ? LabelW1 : LabelW2, LabelH);
        n.label.enableAutoSizing = true;
        n.label.fontSizeMin = 11f;
        n.label.fontSizeMax = 16f;
        n.label.enableWordWrapping = true;
        n.label.overflowMode = TextOverflowModes.Truncate;
        return n;
    }

    // карточка выбранного узла: светлая конфетная панель в цвете состояния, мини-вымпел, цена чипом
    void BuildCard()
    {
        const float tw = W - 24f;
        tip = UiKit.MakeImage(Root, "Card", UiKit.Panel, true, 0.8f);
        TL(tip.rectTransform, 12f, CardY, tw, CardH);
        var t = tip.transform;

        cardPennant = UiKit.MakeImage(t, "CardPennant", UiKit.Banner, false);
        cardPennant.preserveAspect = true;
        Anch(cardPennant.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(58f, 74f));
        tipIcon = UiKit.MakeIcon(cardPennant.transform, UiIcon.Star, 34f);
        tipIcon.rectTransform.anchoredPosition = new Vector2(0f, 3f);

        tipTitle = UiKit.MakeLabel(t, "", 26f, UiCandy.Ink, TextAlignmentOptions.Left, true);
        TL(tipTitle.rectTransform, 92f, 12f, 320f, 34f);
        AutoSize(tipTitle, 14f, 26f);

        tipSub = UiKit.MakeLabel(t, "", 17f, UiCandy.Muted, TextAlignmentOptions.Left, false);
        TL(tipSub.rectTransform, 92f, 48f, 320f, 26f);
        AutoSize(tipSub, 11f, 17f);

        // описание на всю ширину: до трёх строк мелким шрифтом, кнопка справа ниже
        tipDesc = UiKit.MakeLabel(t, "", 20f, UiCandy.Ink, TextAlignmentOptions.TopLeft, false);
        TL(tipDesc.rectTransform, 16f, 92f, tw - 32f, 44f);
        tipDesc.enableAutoSizing = true;
        tipDesc.fontSizeMin = 13f;
        tipDesc.fontSizeMax = 20f;
        tipDesc.enableWordWrapping = true;
        tipDesc.overflowMode = TextOverflowModes.Truncate;

        tipStatus = UiKit.MakeLabel(t, "", 18f, UiCandy.Muted, TextAlignmentOptions.Left, true);
        TL(tipStatus.rectTransform, 16f, 146f, 360f, 26f);
        AutoSize(tipStatus, 11f, 18f);

        // цена в очках: чип со звездой (очки, не монеты) сверху справа, кнопка снизу справа
        tipCost = UiCandy.Chip(t, UiIcon.Star, "1 очко", 40f, UiTone.Neutral);
        Anch(tipCost.rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(0f, 40f));

        tipBtn = UiKit.MakeButton(t, "Изучить", UiBtn.Yellow, new Vector2(190f, 52f), 24f, 1f);
        Anch(tipBtn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 18f), new Vector2(190f, 52f));
        tipBtnTxt = tipBtn.GetComponentInChildren<TMP_Text>();
        tipBtn.onClick.AddListener(BuySelected);
    }

    // подтверждение сброса: затемняет вкладку и ловит клики, чтобы под ним ничего не нажималось
    void BuildConfirm()
    {
        var ov = UiKit.MakeImage(Root, "Confirm", null, false);
        ov.color = new Color(0.17f, 0.18f, 0.36f, 0.5f);   // мягкий индиго вместо чёрного
        ov.raycastTarget = true;
        Stretch(ov.rectTransform);
        confirmRoot = ov.gameObject;

        var box = UiKit.MakePanel(ov.transform, new Vector2(580f, 350f), false, 1f, "ConfirmBox");
        box.raycastTarget = true;
        Anch(box.rectTransform, Mid, Mid, Vector2.zero, new Vector2(580f, 350f));

        var title = UiKit.MakeLabel(box.transform, "Сбросить навыки?", 34f, UiCandy.Ink, TextAlignmentOptions.Center, true);
        Anch(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(540f, 44f));

        confirmText = UiKit.MakeLabel(box.transform, "", 24f, UiCandy.Ink, TextAlignmentOptions.Center, false);
        Anch(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(520f, 104f));
        confirmText.enableWordWrapping = true;

        confirmChip = UiCandy.Chip(box.transform, UiIcon.Coin, "0", 40f, UiTone.Peach);
        Anch(confirmChip.rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(0f, 40f));

        confirmYes = UiKit.MakeButton(box.transform, "Сбросить", UiBtn.Amber, new Vector2(250f, 66f), 28f, 1f);
        Anch(confirmYes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-135f, 32f), new Vector2(250f, 66f));
        confirmYes.onClick.AddListener(ConfirmReset);

        var no = UiKit.MakeButton(box.transform, "Отмена", UiBtn.Gray, new Vector2(250f, 66f), 28f, 1f);
        Anch(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(135f, 32f), new Vector2(250f, 66f));
        no.onClick.AddListener(() => confirmRoot.SetActive(false));

        confirmRoot.SetActive(false);
    }

    // ------------------------------------------------------------ мелкие помощники
    static void TR(RectTransform r, float right, float top, float w, float h)
    {
        Anch(r, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-right, -top), new Vector2(w, h));
    }
}
