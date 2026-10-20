// Assets/_Project/Scripts/UI/SkillTreeUi.cs (НЕ в Editor)
// Правая панель окна прокачки (UpgradeWindow): дерево навыков на месте старых талантов. Всё строится из кода в стиле UiKit,
// настраивать в сцене ничего не нужно. Окно создаёт этот компонент само (UpgradeWindow.Build) и даёт ему панель 682 x 807.
//   Вверху: «Очков: N» и кнопка «Сбросить навыки» (цена из PlayerSkills.ResetCost, с подтверждением).
//   Три колонки-ветки (Лесоруб, Носильщик, Хозяин), в каждой три яруса: ярус I один узел, ярусы II и III два узла-альтернативы.
//   Тап по узлу выбирает его и показывает карточку внизу (название, описание, цена, статус), кнопка «Изучить» покупает.
//   Состояния узла: куплен (галочка), можно купить (зелёное свечение), не хватает очков, закрыт (замок), занят выбором соседа (крест).
// Логика только в PlayerSkills (CanBuy / TryBuy / TryReset / GetState), окно лишь показывает и вызывает её.
// Подписка на PlayerSkills.Changed в OnEnable/OnDisable. MobileInput.ModalOpen держит само окно прокачки (UpgradeWindow.IsOpen),
// отдельно его ставить не нужно. Шрифт берётся из UIManager.objectiveText.font через UiKit.MakeLabel.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static UiLayout;

public sealed class SkillTreeUi : MonoBehaviour
{
    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080, область 1322 x 807)
    const float W = 682f, H = 807f;
    const float NodeSize = 80f, NodeOff = 48f, T = 6f;             // узел, сдвиг левого и правого узла от оси, толщина линии
    const float LabelW1 = 200f, LabelW2 = 90f, LabelH = 38f;       // ширина подписи узла (ярус I / ярусы II и III) и её высота (две строки)
    static readonly float[] TierY = { 205f, 356f, 510f };          // центры ярусов
    static readonly float[] ColX = { 114f, 341f, 568f };           // оси веток
    const float CardY = 606f, CardH = 190f;

    static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    static readonly Color Bad = new Color32(0xE0, 0x6A, 0x5A, 255);
    static readonly Color LineOn = new Color32(0xF2, 0xB1, 0x34, 255);
    static readonly Color LineOff = new Color32(0x3A, 0x4A, 0x60, 255);
    static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
    static readonly Color[] BranchColors =
    {
        new Color32(0x3F, 0x93, 0x47, 255), new Color32(0x3D, 0x7E, 0xBF, 255), new Color32(0xCC, 0x4A, 0x2E, 255)
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

    class Node
    {
        public SkillNodeDef def;
        public RectTransform rt;
        public Image glow, frame, icon, lockImg, check, cross;
        public TMP_Text label;
        public SkillNodeState state;
    }

    class Line { public Image img; public string needId; }   // needId: узел, который должен быть куплен, чтобы линия загорелась (null: всегда)

    public RectTransform Root { get; private set; }
    public bool ConfirmOpen { get { return confirmRoot != null && confirmRoot.activeSelf; } }

    bool built;
    string selId;
    string flashId;
    float flashUntil;
    readonly List<Node> nodes = new List<Node>();
    readonly List<Line> lines = new List<Line>();

    TMP_Text pointsText, nextText, resetPrice, resetBtnTxt;
    Button resetBtn;

    Image tipIcon;
    TMP_Text tipTitle, tipSub, tipDesc, tipStatus, tipCost, tipBtnTxt;
    Image tipStar;
    Button tipBtn;

    GameObject confirmRoot;
    TMP_Text confirmText;
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
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
        bool hoverFx = !Application.isMobilePlatform;
        Vector2 mouse = Input.mousePosition;

        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            Color gc;
            if (n.def.Id == flashId && t < flashUntil) gc = new Color(UiKit.BrassHi.r, UiKit.BrassHi.g, UiKit.BrassHi.b, 0.95f);
            else if (n.def.Id == selId) gc = new Color(1f, 1f, 1f, 0.40f + 0.30f * pulse);
            else if (n.state == SkillNodeState.Bought) gc = new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.45f);
            else if (n.state == SkillNodeState.Available) gc = new Color(UiKit.Good.r, UiKit.Good.g, UiKit.Good.b, 0.25f + 0.20f * pulse);
            else gc = Clear;
            n.glow.color = gc;

            if (hoverFx)
            {
                bool hover = RectTransformUtility.RectangleContainsScreenPoint(n.rt, mouse, null);
                float s = Mathf.MoveTowards(n.rt.localScale.x, hover ? 1.08f : 1f, Time.unscaledDeltaTime * 1.2f);
                n.rt.localScale = Vector3.one * s;
            }
        }
    }

    // ------------------------------------------------------------ действия
    void Select(string id)
    {
        selId = id;
        Refresh();
    }

    void BuySelected()
    {
        if (string.IsNullOrEmpty(selId)) return;
        if (PlayerSkills.TryBuy(selId)) { flashId = selId; flashUntil = Time.unscaledTime + 0.6f; }
        Refresh();
    }

    void OpenConfirm()
    {
        string reason;
        if (!PlayerSkills.CanReset(out reason)) return;
        int cost = PlayerSkills.ResetCost;
        confirmText.text = cost > 0
            ? "Все навыки будут сняты, очки вернутся.\nСброс стоит <color=#FFD66B>" + cost + " монет</color>."
            : "Все навыки будут сняты, очки вернутся.\nПервый сброс <color=#6DBE6A>бесплатный</color>, дальше он стоит "
              + GameBalance.SkillResetCoins + " монет.";
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

    public void Refresh()
    {
        if (!built) return;

        int free = PlayerSkills.FreePoints, total = PlayerSkills.TotalPoints;
        Set(pointsText, "Очков: <color=#" + (free > 0 ? "6DBE6A" : "FFD66B") + ">" + free + "</color>");
        int level = PlayerStats.Level;
        Set(nextText, total >= GameBalance.SkillMaxPoints ? "Все очки получены (" + total + " из " + GameBalance.SkillMaxPoints + ")"
                                                         : "Всего " + total + " из " + GameBalance.SkillMaxPoints + ", следующее на " + (level + 1) + "-м уровне");

        // кнопка сброса и цена
        string rr;
        bool canReset = PlayerSkills.CanReset(out rr);
        int cost = PlayerSkills.ResetCost;
        Set(resetPrice, cost > 0 ? "<color=#FFD66B>" + cost + " монет</color>" : "<color=#6DBE6A>первый раз бесплатно</color>");
        resetBtn.interactable = canReset;
        resetBtnTxt.color = canReset ? UiKit.Navy : new Color(0.86f, 0.88f, 0.92f, 1f);

        // узлы и линии
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            n.state = PlayerSkills.GetState(n.def.Id);
            bool closed = n.state == SkillNodeState.Locked || n.state == SkillNodeState.Blocked;
            n.frame.color = closed ? new Color(0.55f, 0.58f, 0.66f, 1f) : Color.white;
            n.icon.color = closed ? new Color(0.42f, 0.45f, 0.52f, 1f)
                         : n.state == SkillNodeState.NoPoints ? new Color(1f, 1f, 1f, 0.78f) : Color.white;
            n.lockImg.gameObject.SetActive(n.state == SkillNodeState.Locked);
            n.cross.gameObject.SetActive(n.state == SkillNodeState.Blocked);
            n.check.gameObject.SetActive(n.state == SkillNodeState.Bought);
            n.label.color = n.state == SkillNodeState.Bought ? UiKit.BrassHi : closed ? Dim : Color.white;
        }
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            l.img.color = l.needId == null || PlayerSkills.Has(l.needId) ? LineOn : LineOff;
        }

        RefreshCard();
    }

    void RefreshCard()
    {
        var def = SkillTree.Get(selId);
        if (def == null) return;

        tipIcon.sprite = UiKit.Icon(IconFor(def.Id));
        Set(tipTitle, def.Title);
        Set(tipSub, SkillTree.BranchName(def.Branch) + ", ярус " + TierNames[def.Tier - 1] + " · с " + def.RequiredLevel + "-го уровня");
        Set(tipDesc, def.Description);

        var st = PlayerSkills.GetState(def.Id);
        string reason;
        bool can = PlayerSkills.CanBuy(def.Id, out reason);
        string status;
        switch (st)
        {
            case SkillNodeState.Bought: status = "<color=#6DBE6A>Изучено</color>"; break;
            case SkillNodeState.Available: status = "<color=#6DBE6A>Можно изучить</color>"; break;
            case SkillNodeState.NoPoints: status = "<color=#E06A5A>" + reason + "</color>"; break;
            case SkillNodeState.Blocked: status = "<color=#E06A5A>" + reason + "</color>"; break;
            default: status = reason; break;   // Locked: «Нужен 4 уровень» или «Сначала изучите …»
        }
        Set(tipStatus, status);

        bool bought = st == SkillNodeState.Bought;
        tipStar.gameObject.SetActive(!bought);
        tipCost.gameObject.SetActive(!bought);
        Set(tipCost, def.Cost + " " + UiDraw.Plural(def.Cost, "очко", "очка", "очков"));
        tipCost.color = PlayerSkills.FreePoints >= def.Cost ? UiKit.BrassHi : Bad;

        Set(tipBtnTxt, bought ? "Изучено" : "Изучить");
        tipBtnTxt.color = can ? UiKit.Navy : new Color(0.86f, 0.88f, 0.92f, 1f);
        tipBtn.interactable = can;
    }

    // ------------------------------------------------------------ сборка UI
    // parent: правая панель окна (w x h = 682 x 807). Возвращает корень дерева.
    public RectTransform Build(Transform parent, float w, float h)
    {
        if (built) return Root;

        Root = NewRT("SkillsTree", parent);
        Stretch(Root);

        BuildHeader();

        var all = SkillTree.All;
        for (int b = 0; b < 3; b++) BuildBranchHeader(b);

        // линии рисуем до узлов, чтобы узлы лежали сверху
        for (int b = 0; b < 3; b++) BuildLines(b);

        // узлы: ярус I по центру, ярусы II и III: первый узел группы слева, второй справа
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

        selId = SkillTree.All.Count > 0 ? SkillTree.All[0].Id : null;
        built = true;
        Refresh();
        return Root;
    }

    void BuildHeader()
    {
        var star = UiKit.MakeIcon(Root, UiIcon.Star, 36f, "PointsIcon");
        TLC(star.rectTransform, 34f, 28f, 36f, 36f);
        pointsText = UiKit.MakeLabel(Root, "", 28f, Color.white, TextAlignmentOptions.Left, true);
        TL(pointsText.rectTransform, 60f, 8f, 250f, 40f);
        AutoSize(pointsText, 16f, 28f);
        nextText = UiKit.MakeLabel(Root, "", 17f, Soft, TextAlignmentOptions.Left, false);
        TL(nextText.rectTransform, 16f, 54f, 350f, 28f);
        AutoSize(nextText, 11f, 17f);

        // сброс справа: кнопка, под ней цена
        resetBtn = UiKit.MakeButton(Root, "Сбросить навыки", UiBtn.Amber, new Vector2(240f, 52f), 22f, 1f);
        TR(resetBtn.GetComponent<RectTransform>(), 16f, 8f, 240f, 52f);
        resetBtnTxt = resetBtn.GetComponentInChildren<TMP_Text>();
        AutoSize(resetBtnTxt, 13f, 22f);
        resetBtn.onClick.AddListener(OpenConfirm);

        resetPrice = UiKit.MakeLabel(Root, "", 17f, Soft, TextAlignmentOptions.Right, false);
        TR(resetPrice.rectTransform, 16f, 62f, 260f, 24f);
        AutoSize(resetPrice, 11f, 17f);
    }

    void BuildBranchHeader(int b)
    {
        float cx = ColX[b];
        var bar = UiKit.MakeImage(Root, "StripBar" + b, null, false);
        bar.color = UiKit.Brass;
        TLC(bar.rectTransform, cx, 143f, 218f, 5f);

        var strip = UiKit.MakeImage(Root, "Strip" + b, null, false);
        strip.color = BranchColors[b];
        TLC(strip.rectTransform, cx, 120f, 212f, 40f);

        var ic = UiKit.MakeIcon(strip.transform, BranchIcons[b], 28f, "BranchIcon");
        Anch(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(28f, 28f));
        var nm = UiKit.MakeLabel(strip.transform, SkillTree.BranchName((SkillBranch)b).ToUpperInvariant(), 20f, Color.white, TextAlignmentOptions.Center, true);
        nm.rectTransform.offsetMin = new Vector2(40f, 0f);
        nm.rectTransform.offsetMax = new Vector2(-6f, 0f);
        AutoSize(nm, 11f, 20f);
    }

    void BuildLines(int b)
    {
        float cx = ColX[b];
        var root = SkillTree.Tier1Of((SkillBranch)b);
        string rootId = root != null ? root.Id : null;
        float half = NodeSize * 0.5f;

        // короткий отрезок от заголовка до узла I
        AddLine(cx, 146f, cx, TierY[0] - half, null);

        // ствол от подписи узла I вниз до развилки яруса III (проходит между узлами яруса II)
        float label1Bottom = TierY[0] + half + 4f + LabelH;
        float ym2 = (label1Bottom + TierY[1] - half) * 0.5f;
        float ym3 = (TierY[1] + half + 4f + LabelH + TierY[2] - half) * 0.5f;   // между подписями яруса II и узлами яруса III
        AddLine(cx, label1Bottom, cx, ym3, rootId);

        // развилки: перекладина зажигается корнем ветки, ножки к узлам зажигаются самим узлом
        var all = SkillTree.All;
        for (int tier = 2; tier <= 3; tier++)
        {
            float ym = tier == 2 ? ym2 : ym3, ny = TierY[tier - 1] - half;
            AddLine(cx - NodeOff, ym, cx + NodeOff, ym, rootId);
            int k = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if ((int)all[i].Branch != b || all[i].Tier != tier) continue;
                float nx = cx + (k == 0 ? -NodeOff : NodeOff);
                AddLine(nx, ym, nx, ny, all[i].Id);
                k++;
            }
        }
    }

    void AddLine(float x1, float y1, float x2, float y2, string needId)
    {
        var im = UiKit.MakeImage(Root, "Line", null, false);
        im.color = LineOff;
        TLC(im.rectTransform, (x1 + x2) * 0.5f, (y1 + y2) * 0.5f, Mathf.Abs(x2 - x1) + T, Mathf.Abs(y2 - y1) + T);
        lines.Add(new Line { img = im, needId = needId });
    }

    Node BuildNode(SkillNodeDef d, float cx, float cy)
    {
        var n = new Node { def = d };

        n.glow = UiKit.MakeImage(Root, "NodeGlow_" + d.Id, UiKit.Slot, true, 1f);
        n.glow.color = Clear;
        TLC(n.glow.rectTransform, cx, cy, NodeSize + 22f, NodeSize + 22f);

        n.frame = UiKit.MakeSlot(Root, NodeSize, 1f, "Node_" + d.Id);
        n.rt = n.frame.rectTransform;
        TLC(n.rt, cx, cy, NodeSize, NodeSize);
        string id = d.Id;
        AddBtn(n.frame, () => Select(id));

        n.icon = UiKit.MakeIcon(n.frame.transform, IconFor(d.Id), 54f);
        n.lockImg = UiKit.MakeIcon(n.frame.transform, UiIcon.Lock, 36f, "Lock");
        n.lockImg.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        n.cross = UiKit.MakeIcon(n.frame.transform, UiIcon.Cross, 36f, "Blocked");
        n.cross.color = Bad;
        n.check = UiKit.MakeIcon(n.frame.transform, UiIcon.Check, 28f, "Check");
        Anch(n.check.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-6f, 8f), new Vector2(28f, 28f));

        // название под узлом: до двух строк (колонки узкие)
        n.label = UiKit.MakeLabel(Root, d.Title, 16f, Color.white, TextAlignmentOptions.Top, true);
        TLC(n.label.rectTransform, cx, cy + NodeSize * 0.5f + 4f + LabelH * 0.5f, d.Tier == 1 ? LabelW1 : LabelW2, LabelH);
        n.label.enableAutoSizing = true;
        n.label.fontSizeMin = 11f;
        n.label.fontSizeMax = 16f;
        n.label.enableWordWrapping = true;
        n.label.overflowMode = TextOverflowModes.Truncate;
        return n;
    }

    void BuildCard()
    {
        const float tw = W - 24f;
        var tip = UiKit.MakeImage(Root, "Card", UiKit.Slot, true, 1f);
        TL(tip.rectTransform, 12f, CardY, tw, CardH);
        var t = tip.transform;

        var slot = UiKit.MakeSlot(t, 76f, 1f, "CardSlot");
        Anch(slot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(76f, 76f));
        tipIcon = UiKit.MakeIcon(slot.transform, UiIcon.Star, 52f);

        tipTitle = UiKit.MakeLabel(t, "", 26f, UiKit.BrassHi, TextAlignmentOptions.Left, true);
        TL(tipTitle.rectTransform, 102f, 12f, 330f, 34f);
        AutoSize(tipTitle, 14f, 26f);

        tipSub = UiKit.MakeLabel(t, "", 17f, Dim, TextAlignmentOptions.Left, false);
        TL(tipSub.rectTransform, 102f, 48f, 330f, 26f);
        AutoSize(tipSub, 11f, 17f);

        // описание на всю ширину под иконкой: до трёх строк, кнопка справа ниже
        tipDesc = UiKit.MakeLabel(t, "", 20f, Color.white, TextAlignmentOptions.TopLeft, false);
        TL(tipDesc.rectTransform, 14f, 98f, tw - 28f, 52f);
        tipDesc.enableAutoSizing = true;
        tipDesc.fontSizeMin = 13f;
        tipDesc.fontSizeMax = 20f;
        tipDesc.enableWordWrapping = true;
        tipDesc.overflowMode = TextOverflowModes.Truncate;

        tipStatus = UiKit.MakeLabel(t, "", 18f, Soft, TextAlignmentOptions.Left, false);
        TL(tipStatus.rectTransform, 14f, 152f, 330f, 28f);
        AutoSize(tipStatus, 11f, 18f);

        // цена сверху справа, кнопка снизу справа
        tipStar = UiKit.MakeIcon(t, UiIcon.Star, 28f, "CostStar");
        Anch(tipStar.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-(14f + 116f), -16f), new Vector2(28f, 28f));
        tipCost = UiKit.MakeLabel(t, "", 22f, UiKit.BrassHi, TextAlignmentOptions.Left, true);
        Anch(tipCost.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(112f, 32f));
        AutoSize(tipCost, 12f, 22f);

        tipBtn = UiKit.MakeButton(t, "Изучить", UiBtn.Amber, new Vector2(190f, 54f), 24f, 1f);
        Anch(tipBtn.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 10f), new Vector2(190f, 54f));
        tipBtnTxt = tipBtn.GetComponentInChildren<TMP_Text>();
        tipBtn.onClick.AddListener(BuySelected);
    }

    // подтверждение сброса: затемняет вкладку и ловит клики, чтобы под ним ничего не нажималось
    void BuildConfirm()
    {
        var ov = UiKit.MakeImage(Root, "Confirm", null, false);
        ov.color = new Color(0f, 0f, 0f, 0.65f);
        ov.raycastTarget = true;
        Stretch(ov.rectTransform);
        confirmRoot = ov.gameObject;

        var box = UiKit.MakePanel(ov.transform, new Vector2(580f, 330f), false, 1f, "ConfirmBox");
        box.raycastTarget = true;
        Anch(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 330f));

        var title = UiKit.MakeLabel(box.transform, "Сбросить навыки?", 34f, UiKit.BrassHi, TextAlignmentOptions.Center, true);
        Anch(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(540f, 44f));

        confirmText = UiKit.MakeLabel(box.transform, "", 24f, Color.white, TextAlignmentOptions.Center, false);
        Anch(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(520f, 120f));
        confirmText.enableWordWrapping = true;

        confirmYes = UiKit.MakeButton(box.transform, "Сбросить", UiBtn.Amber, new Vector2(250f, 66f), 28f, 1f);
        Anch(confirmYes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-135f, 28f), new Vector2(250f, 66f));
        confirmYes.onClick.AddListener(ConfirmReset);

        var no = UiKit.MakeButton(box.transform, "Отмена", UiBtn.Gray, new Vector2(250f, 66f), 28f, 1f);
        Anch(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(135f, 28f), new Vector2(250f, 66f));
        no.onClick.AddListener(() => confirmRoot.SetActive(false));

        confirmRoot.SetActive(false);
    }

    // ------------------------------------------------------------ мелкие помощники (те же, что в UpgradeWindow)
    static void AddBtn(Image img, UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(a);
    }

    static void TR(RectTransform r, float right, float top, float w, float h)
    {
        Anch(r, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-right, -top), new Vector2(w, h));
    }
}
