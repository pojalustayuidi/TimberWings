// Assets/_Project/Scripts/UI/Progression/AchievementsWindow.Build.cs (НЕ в Editor)
// ЧАСТЬ класса AchievementsWindow (partial): сборка окна, раскладка вкладки, карточка выбранного.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: AchievementsWindow.cs.
// Остальные части: AchievementsWindow.Anim.cs, AchievementsWindow.TabsData.cs, AchievementsWindow.HudHelpers.cs.
// Методы этой части: Build, ConfigScaler, BuildHeader, BuildTabs, BuildGraph, BuildNode, MakeFlow, Part, LayoutCat, PlaceEdge, SetSeg, BuildDetail.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;
using static AchUi;

public partial class AchievementsWindow
{
    // ============================================================ сборка окна
    void Build()
    {
        if (built) return;
        var cgo = new GameObject("AchievementsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Window;
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
}
