#if UNITY_EDITOR
// Assets/_Project/Editor/UI/BuilderSimHud.cs   (ЗАМЕНИТЬ старый)
// Меню: Tools > BuilderSim > Build HUD
// Одна кнопка собирает ВЕСЬ HUD сразу в новом дизайне (UiKit) и сохраняет его в сцене:
//   1) каркас HUD_Auto (уровень, задание, мини-карта, большая карта, подсказка [E]);
//   2) HudKitBuilder.Skin: дизайн UiKit для уровня, задания, карт и подсказки (раньше это делал HudKitSkin при старте игры);
//   3) ResourcePanelBuilder: общая панель ресурсов (монеты, дерево, доски).
// Старые синие панели монет и дерева больше не создаются.
// Нужны рядом (Assets/_Project/Scripts/UI/): HudPop.cs, HudShine.cs, HudIntro.cs, HudMinimap.cs (v2), ActionPrompt.cs,
// ZoneToast.cs, MapZone.cs, MapPoi.cs.   Версия под HudMinimap v2: маска, пинг, название зоны, кнопки карты, шаблон меток.
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static EditorAssets;

public class BuilderSimHud : ScriptableWizard
{
    public enum MapCorner { TopRight, BottomLeft }

    [Header("Необязательно: свои иконки (пусто = нарисую)")]
    public Sprite coinIcon;
    public Sprite woodIcon;
    [Header("Необязательно: шрифт (пусто = как в твоём старом UI)")]
    public TMP_FontAsset font;

    [Header("Что добавить")]
    public bool addMinimap = true;
    [Tooltip("TopRight: под панелью уровня (на телефоне не мешает джойстику). BottomLeft: снизу слева")]
    public MapCorner minimapCorner = MapCorner.TopRight;
    public float minimapSize = 250f;
    public bool addVignette = true;
    [Tooltip("Плашка внизу по центру: [E] Рубить (ActionPrompt)")]
    public bool addActionPrompt = true;

    const string Dir = "Assets/_Game/Art/UI";

    // ---------------------------------------------------------------- палитра
    static readonly Color PanelFill = new Color32(0x2C, 0x3E, 0x68, 245);
    static readonly Color PanelRing = new Color32(0x6F, 0x86, 0xC0, 255);
    static readonly Color Outline = new Color32(0x0C, 0x12, 0x22, 255);
    static readonly Color BadgeFill = new Color32(0x3A, 0x4F, 0x80, 255);
    static readonly Color TrackCol = new Color32(0x10, 0x18, 0x2C, 255);
    static readonly Color Green = new Color32(0x86, 0xE0, 0x6E, 255);
    static readonly Color Accent = new Color32(0x6B, 0xCB, 0x5A, 255);
    static readonly Color KeyFill = new Color32(0x4B, 0x63, 0x9E, 255);
    static readonly Color Soft = new Color32(0xB9, 0xC8, 0xEA, 255);
    static readonly Color GoldRing = new Color32(0xF5, 0xC5, 0x42, 255);
    static readonly Color WoodRing = new Color32(0xC9, 0x8E, 0x58, 255);
    static readonly Color TextMain = Color.white;

    Sprite spPanel, spPanelGrad, spPanelRing, spPanelRim, spPanelGloss, spShadow;
    Sprite spPill, spWhite, spBar, spShine, spVignette;
    Sprite spRoundGrad, spRoundRing, spRoundGloss, spRoundShadow;
    Sprite spStar, spArrow, spMapRing;
    Sprite spTent, spHut, spMarket, spQuest, spCamp, spBridge, spForest, spGeneric;
    Material outlineMat;

    [MenuItem("Tools/BuilderSim/UI/Build HUD", false, 900)]
    static void Open()
    {
        var w = DisplayWizard<BuilderSimHud>("BuilderSim HUD", "Построить");
        w.helpString = "Создаст новый Canvas HUD_Auto сразу в новом дизайне: ресурсы, уровень, задание, мини-карта, подсказка. Всё хранится в сцене, в игре ничего не перекрашивается. Старый UI не удаляется.";
    }

    void OnWizardCreate() { Build(); }

    // ================================================================ сборка
    void Build()
    {
        EnsureFolder("Assets/_Game");
        EnsureFolder("Assets/_Game/Art");
        EnsureFolder(Dir);

        var ui = FindUiManager();
        if (font == null && ui != null)
        {
            var p = new SerializedObject(ui).FindProperty("objectiveText");
            if (p != null && p.objectReferenceValue is TMP_Text old) font = old.font;
        }

        MakeSprites();
        outlineMat = MakeOutlineMaterial();

        var oldHud = GameObject.Find("HUD_Auto");
        if (oldHud != null) Undo.DestroyObjectImmediate(oldHud);

        var root = new GameObject("HUD_Auto", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(root, "Build HUD");
        root.layer = 5;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var rt = (RectTransform)root.transform;
        var intro = root.AddComponent<HudIntro>();

        if (addVignette) Vignette(rt);

        // ---- монеты, дерево и доски: общая панель ResourcePanel собирается ниже (ResourcePanelBuilder), старые «таблетки» не строим

        // ---- задание: по центру сверху
        var task = Panel("Task", rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(820, 92), PanelFill, true, 40f, 1.6f);
        var qi = NewRect("QuestIcon", task);
        Anchor(qi, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(66f, 66f));
        Img(qi, spQuest, Color.white, Image.Type.Simple).preserveAspect = true;
        var thsh = task.GetComponent<HudShine>();
        if (thsh != null) { thsh.breathe = qi; thsh.breatheAmount = 0.07f; }

        var chip = Panel("Chip", task, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 38), Accent, false, 24f);
        var chipLbl = NewRect("Label", chip);
        Stretch(chipLbl, 0, 0, 0, 0);
        Txt(chipLbl, "ЗАДАНИЕ", 20, TrackCol, TextAlignmentOptions.Center, false);

        var taskRect = NewRect("Text", task);
        Stretch(taskRect, 70, 26, 30, 8);
        var objTxt = Txt(taskRect, "ЗАДАНИЕ", 28, TextMain, TextAlignmentOptions.Center, true);
        objTxt.enableAutoSizing = true;
        objTxt.fontSizeMin = 20;
        objTxt.fontSizeMax = 30;
        AddIntro(intro, task, new Vector2(0f, 170f), 0.2f);

        // ---- уровень и опыт: справа сверху
        var lvl = Panel("Level", rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(340, 88), PanelFill, true, 40f, 2.4f);
        var star = Badge(lvl, 20f, 96f, spStar, 64f, GoldRing);
        var lhs = lvl.GetComponent<HudShine>();
        if (lhs != null) { lhs.breathe = star.Find("Icon"); lhs.breatheAmount = 0.07f; }
        var lvlRect = NewRect("Text", lvl);
        lvlRect.anchorMin = new Vector2(0, 1); lvlRect.anchorMax = new Vector2(1, 1);
        lvlRect.offsetMin = new Vector2(80, -56); lvlRect.offsetMax = new Vector2(-22, -8);
        var lvlTxt = Txt(lvlRect, "LVL 1", 32, TextMain, TextAlignmentOptions.Center, true);
        var xpFill = Bar(lvl, 80f, 24f, 14f, 20f, 0.25f);
        AddIntro(intro, lvl, new Vector2(0f, 170f), 0.28f);

        // ---- мини-карта
        if (addMinimap) BuildMinimap(rt, intro);
        if (addActionPrompt) BuildActionPrompt(rt);

        AddPop(lvlTxt);

        Hook(ui, null, null, lvlTxt, xpFill, objTxt);     // coinText / woodText назначит ResourcePanelBuilder

        // ---- новый дизайн прямо в сцене
        var uiMgr = ui as UIManager;
        if (uiMgr != null)
        {
            HudKitBuilder.Skin(rt, uiMgr);
            var resources = ResourcePanelBuilder.BuildQuiet();
            if (resources != null) resources.SetSiblingIndex(addVignette ? 1 : 0);     // под большой картой и подсказкой, как были старые панели
        }
        else Debug.LogWarning("BuilderSim: UIManager не найден, дизайн и панель ресурсов не применены. Добавь UIManager и запусти Tools > BuilderSim > Skin Existing HUD (Kit) и Build Resource Panel.");

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("BuilderSim: HUD_Auto построен сразу в новом дизайне (ресурсы, уровень, задание, карта, подсказка). Отключи старые панели в UI (но не сам UI_Manager) и сохрани сцену (Ctrl+S).");
    }

    static void AddIntro(HudIntro intro, RectTransform rt, Vector2 offset, float delay)
    {
        if (intro == null || rt == null) return;
        if (intro.items == null) intro.items = new System.Collections.Generic.List<HudIntro.Item>();
        intro.items.Add(new HudIntro.Item { rt = rt, offset = offset, delay = delay });
    }

    // ================================================================ блоки HUD
    void Vignette(RectTransform parent)
    {
        var v = NewRect("Vignette", parent);
        Stretch(v, 0, 0, 0, 0);
        Img(v, spVignette, new Color(0.02f, 0.04f, 0.1f, 0.38f), Image.Type.Simple);
    }

    RectTransform Pill(string name, Transform parent, float x, Sprite icon, Color ring, out TextMeshProUGUI value, float delay)
    {
        var p = Panel(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -40), new Vector2(220, 80), PanelFill, true, 40f, delay);
        var badge = Badge(p, 20f, 88f, icon, 54f, ring);
        var t = NewRect("Value", p);
        Stretch(t, 76, 0, 18, 0);
        t.pivot = new Vector2(0, 0.5f);   // «подпрыгивание» идёт от левого края
        value = Txt(t, "0", 42, TextMain, TextAlignmentOptions.MidlineLeft, true);
        var hs = p.GetComponent<HudShine>();
        if (hs != null) hs.breathe = badge.Find("Icon");
        return p;
    }

    void BuildMinimap(RectTransform root, HudIntro intro)
    {
        float S = minimapSize;
        Vector2 anchor, pivot, pos;
        Vector2 introOffset;
        if (minimapCorner == MapCorner.TopRight)
        {
            // под панелью уровня; зазор оставлен для плашки «+N XP»
            anchor = new Vector2(1, 1); pivot = new Vector2(1, 1); pos = new Vector2(-40, -198);
            introOffset = new Vector2(260f, 0f);
        }
        else
        {
            anchor = new Vector2(0, 0); pivot = new Vector2(0, 0); pos = new Vector2(40, 64);
            introOffset = new Vector2(-260f, 0f);
        }

        var m = NewRect("Minimap", root);
        Anchor(m, anchor, pivot, pos, new Vector2(S, S));

        var sh = NewRect("Shadow", m);
        float marg = S * 0.2273f;
        sh.anchorMin = Vector2.zero; sh.anchorMax = Vector2.one;
        sh.offsetMin = new Vector2(-marg, -marg - 8f); sh.offsetMax = new Vector2(marg, marg - 8f);
        Img(sh, spRoundShadow, new Color(0, 0, 0, 0.6f), Image.Type.Simple);

        var disk = NewRect("Disk", m);
        Stretch(disk, 0, 0, 0, 0);
        Img(disk, spRoundGrad, Outline, Image.Type.Simple);

        var clip = NewRect("Clip", m);
        Stretch(clip, 11, 11, 11, 11);
        Img(clip, spRoundGrad, Color.white, Image.Type.Simple);
        var mask = clip.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var map = NewRect("Map", clip);
        Stretch(map, 0, 0, 0, 0);
        var raw = map.gameObject.AddComponent<RawImage>();
        raw.color = new Color(0.17f, 0.30f, 0.20f, 1f);
        raw.raycastTarget = false;

        var vign = NewRect("Vignette", clip);
        Stretch(vign, 0, 0, 0, 0);
        Img(vign, spVignette, new Color(0.02f, 0.05f, 0.12f, 0.75f), Image.Type.Simple);

        var markers = NewRect("Markers", clip);
        Stretch(markers, 0, 0, 0, 0);

        var pingRt = NewRect("Ping", clip);
        Anchor(pingRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
        var miniPing = Img(pingRt, spRoundRing, new Color(1f, 1f, 1f, 0.6f), Image.Type.Simple);

        var player = NewRect("Player", clip);
        Anchor(player, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
        Img(player, spArrow, Color.white, Image.Type.Simple).preserveAspect = true;

        var frame = NewRect("Frame", m);
        Stretch(frame, 0, 0, 0, 0);
        Img(frame, spMapRing, Color.white, Image.Type.Simple);

        // буква N на рамке
        var north = NewRect("North", m);
        Anchor(north, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, S * 0.5f - 7f), new Vector2(32, 32));
        Img(north, spRoundGrad, Outline, Image.Type.Simple);
        var nIn = NewRect("Fill", north);
        Stretch(nIn, 3, 3, 3, 3);
        Img(nIn, spRoundGrad, Accent, Image.Type.Simple);
        var nTxt = NewRect("Text", north);
        Stretch(nTxt, 0, 0, 0, 0);
        Txt(nTxt, "N", 20, TrackCol, TextAlignmentOptions.Center, false);

        // лента с названием зоны
        var zone = Panel("Zone", m, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(Mathf.Min(230f, S * 0.92f), 40), PanelFill, false, 26f);
        var zt = NewRect("Text", zone);
        Stretch(zt, 12, 0, 12, 0);
        var zoneTxt = Txt(zt, "ЛАГЕРЬ", 22, TextMain, TextAlignmentOptions.Center, true);
        zoneTxt.enableAutoSizing = true;
        zoneTxt.fontSizeMin = 14;
        zoneTxt.fontSizeMax = 22;

        // подсказка клавиши карты
        var hint = Panel("MapHint", m, new Vector2(1, 0), new Vector2(1, 0), new Vector2(2, 6), new Vector2(96, 34), PanelFill, false, 20f);
        var ht = NewRect("Text", hint);
        Stretch(ht, 6, 0, 6, 0);
        Txt(ht, "M · Карта", 18, Soft, TextAlignmentOptions.Center, false);

        // клик по мини-карте открывает большую (невидимая кнопка поверх)
        var openRt = NewRect("OpenButton", m);
        Stretch(openRt, 0, 0, 0, 0);
        var openImg = Img(openRt, spRoundGrad, new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
        openImg.raycastTarget = true;
        var openBtn = openRt.gameObject.AddComponent<Button>();
        openBtn.targetGraphic = openImg;
        openBtn.transition = Selectable.Transition.None;

        // ---- большая карта (поверх всего, по умолчанию выключена)
        var big = NewRect("BigMap", root);
        Stretch(big, 0, 0, 0, 0);
        var bcg = big.gameObject.AddComponent<CanvasGroup>();
        bcg.alpha = 0f;

        var dim = NewRect("Dim", big);
        Stretch(dim, 0, 0, 0, 0);
        var dimImg = Img(dim, spWhite, new Color(0.02f, 0.04f, 0.09f, 0.72f), Image.Type.Simple);
        dimImg.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.transition = Selectable.Transition.None;

        var win = Panel("Window", big, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 860), PanelFill, true, 40f, 0.3f);
        var title = NewRect("Title", win);
        title.anchorMin = new Vector2(0, 1); title.anchorMax = new Vector2(1, 1);
        title.offsetMin = new Vector2(20, -72); title.offsetMax = new Vector2(-20, -14);
        Txt(title, "КАРТА ЛОКАЦИИ", 36, TextMain, TextAlignmentOptions.Center, true);

        // невидимая подложка: клик внутри окна не закрывает карту
        var block = NewRect("Block", win);
        Stretch(block, 0, 0, 0, 0);
        Img(block, spWhite, new Color(1f, 1f, 1f, 0f), Image.Type.Simple).raycastTarget = true;
        block.SetAsFirstSibling();

        // кнопка закрытия в углу окна
        var closeRt = NewRect("Close", win);
        Anchor(closeRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16f, -16f), new Vector2(52f, 52f));
        var closeOut = Img(closeRt, spRoundGrad, Outline, Image.Type.Simple);
        closeOut.raycastTarget = true;
        var closeIn = NewRect("Fill", closeRt);
        Stretch(closeIn, 3, 3, 3, 3);
        Img(closeIn, spRoundGrad, KeyFill, Image.Type.Simple);
        var closeTx = NewRect("Text", closeRt);
        Stretch(closeTx, 0, 0, 0, 0);
        Txt(closeTx, "X", 28, TextMain, TextAlignmentOptions.Center, false);
        var closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeOut;
        closeBtn.transition = Selectable.Transition.None;

        var bclip = NewRect("Clip", win);
        Anchor(bclip, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(700, 700));
        Img(bclip, spPanel, Color.white, Image.Type.Sliced);
        var bmask = bclip.gameObject.AddComponent<Mask>();
        bmask.showMaskGraphic = false;

        var bmap = NewRect("Map", bclip);
        Stretch(bmap, 0, 0, 0, 0);
        var braw = bmap.gameObject.AddComponent<RawImage>();
        braw.color = new Color(0.17f, 0.30f, 0.20f, 1f);
        braw.raycastTarget = false;

        var bvig = NewRect("Vignette", bclip);
        Stretch(bvig, 0, 0, 0, 0);
        Img(bvig, spVignette, new Color(0.02f, 0.05f, 0.12f, 0.45f), Image.Type.Simple);

        var bmarkers = NewRect("Markers", bclip);
        Stretch(bmarkers, 0, 0, 0, 0);

        var bplayer = NewRect("Player", bclip);
        Anchor(bplayer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
        Img(bplayer, spArrow, Color.white, Image.Type.Simple).preserveAspect = true;
        var bpingRt = NewRect("Ping", bplayer);
        Anchor(bpingRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
        var bigPing = Img(bpingRt, spRoundRing, new Color(1f, 1f, 1f, 0.6f), Image.Type.Simple);

        var bring = NewRect("Ring", bclip);
        Stretch(bring, 0, 0, 0, 0);
        Img(bring, spPanelRing, PanelRing, Image.Type.Sliced);

        var bhint = NewRect("Hint", win);
        bhint.anchorMin = new Vector2(0, 0); bhint.anchorMax = new Vector2(1, 0);
        bhint.offsetMin = new Vector2(20, 12); bhint.offsetMax = new Vector2(-20, 52);
        Txt(bhint, "M или Esc — закрыть   ·   колесо мыши над мини-картой — масштаб", 20, Soft, TextAlignmentOptions.Center, false);

        // ---- компонент карты (HudMinimap v2)
        var hm = m.gameObject.AddComponent<HudMinimap>();
        hm.miniMap = raw;
        hm.miniMask = clip;
        hm.miniMarkers = markers;
        hm.miniPlayer = player;
        hm.miniPing = miniPing;
        hm.miniNorth = north;
        hm.zoneLabel = zoneTxt;
        hm.zoneBanner = zone;
        hm.openButton = openBtn;
        hm.closeButton = closeBtn;
        hm.bigGroup = bcg;
        hm.bigWindow = win;
        hm.bigMap = braw;
        hm.bigMarkers = bmarkers;
        hm.bigPlayer = bplayer;
        hm.bigPing = bigPing;

        hm.markerTemplate = BuildMarkerTemplate(m);
        hm.kinds = BuildKinds();
        hm.autoFindByName = true;
        hm.autoIgnoreKinds = new string[0];                              // площадки PAD_* нужны как метки
        hm.bigOnlyKinds = new[] { "PAD_Forest", "PAD_Bridge" };          // только на большой карте
        hm.pinnedKinds = new[] { "PAD_QuestBoard", "PAD_Order" };        // на мини-карте остаются указателем на краю
        hm.miniViewWorld = 36f;

        // клик по затемнению закрывает карту
        UnityEventTools.AddPersistentListener(dimBtn.onClick, hm.Close);

        // большая карта активна, но невидима и не ловит клики (HudMinimap сам включает их при открытии)
        bcg.alpha = 0f;
        bcg.blocksRaycasts = false;
        bcg.interactable = false;
        AddIntro(intro, m, introOffset, 0.4f);
    }

    // ================================================================ метки карты (v2)
    // Шаблон метки: тёмная подложка, цветное кольцо (цвет типа), иконка и подпись (подпись видна только на большой карте)
    RectTransform BuildMarkerTemplate(RectTransform parent)
    {
        var t = NewRect("MarkerTemplate", parent);
        Anchor(t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));

        var back = NewRect("Back", t);
        Stretch(back, 0, 0, 0, 0);
        Img(back, spRoundGrad, new Color(Outline.r, Outline.g, Outline.b, 0.7f), Image.Type.Simple);

        var pin = NewRect("Pin", t);
        Stretch(pin, 0, 0, 0, 0);
        Img(pin, spRoundRing, Color.white, Image.Type.Simple);

        var icon = NewRect("Icon", t);
        Anchor(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
        Img(icon, spGeneric, Color.white, Image.Type.Simple).preserveAspect = true;

        var label = NewRect("Label", t);
        label.anchorMin = label.anchorMax = new Vector2(0.5f, 0f);
        label.pivot = new Vector2(0.5f, 1f);
        label.anchoredPosition = new Vector2(0f, -2f);
        label.sizeDelta = new Vector2(300f, 28f);
        var tx = Txt(label, "Метка", 20, TextMain, TextAlignmentOptions.Center, true);
        tx.textWrappingMode = TextWrappingModes.NoWrap;

        t.gameObject.SetActive(false);
        return t;
    }

    // Ключ типа = имя объекта на сцене (площадки Location Builder называются PAD_...). Названия меток можно менять в инспекторе.
    HudMinimap.MapKind[] BuildKinds()
    {
        return new[]
        {
            Kind("PAD_Tent", "Палатка", spTent, new Color32(0xF2, 0x8C, 0x28, 255), 6f),
            Kind("PAD_Hut", "Хижина лесоруба", spHut, new Color32(0xE0, 0x4F, 0x4F, 255), 6f),
            Kind("PAD_Market", "Рынок", spMarket, new Color32(0x3F, 0xA9, 0xF5, 255), 6f),
            Kind("PAD_QuestBoard", "Доска заданий", spQuest, new Color32(0xF0, 0xB4, 0x1E, 255), 6f),
            Kind("PAD_Forest", "Лес", spForest, new Color32(0x3D, 0xB3, 0x5A, 255), 13f),
            Kind("PAD_Bridge", "Мост", spBridge, new Color32(0x6D, 0x7C, 0xFF, 255), 6f),
            Kind("PAD_Order", "Заказ", spGeneric, new Color32(0x7C, 0x8D, 0xB5, 255), 6f),
            Kind("CAMPFIRE_Point", "Лагерь", spCamp, new Color32(0xFF, 0x6A, 0x2A, 255), 7f)
        };
    }

    static HudMinimap.MapKind Kind(string key, string label, Sprite icon, Color tint, float radius)
    {
        return new HudMinimap.MapKind { key = key, label = label, icon = icon, tint = tint, zoneRadius = radius };
    }

    // ================================================================ плашка действия: [E] Рубить
    void BuildActionPrompt(RectTransform root)
    {
        var p = Panel("ActionPrompt", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(340f, 84f), PanelFill, false, 30f);
        var cg = p.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var key = NewRect("Key", p);
        Anchor(key, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52f, 0f), new Vector2(60f, 60f));
        Img(key, spPanel, Outline, Image.Type.Sliced);
        var kf = NewRect("Fill", key);
        Stretch(kf, 3, 3, 3, 3);
        var keyImg = Img(kf, spPanelGrad, KeyFill, Image.Type.Sliced);
        var kt = NewRect("Text", key);
        Stretch(kt, 0, 0, 0, 0);
        var keyTxt = Txt(kt, "E", 40, TextMain, TextAlignmentOptions.Center, true);

        var lr = NewRect("Label", p);
        Stretch(lr, 98, 0, 20, 0);
        var lblTxt = Txt(lr, "Рубить", 34, TextMain, TextAlignmentOptions.MidlineLeft, true);
        lblTxt.enableAutoSizing = true;
        lblTxt.fontSizeMin = 20;
        lblTxt.fontSizeMax = 34;

        var ap = p.gameObject.AddComponent<ActionPrompt>();
        ap.group = cg;
        ap.keyRect = key;
        ap.keyImage = keyImg;
        ap.keyColor = KeyFill;
        ap.keyText = keyTxt;
        ap.labelText = lblTxt;
    }

    // ================================================================ элементы UI
    RectTransform ShadowRect(Transform parent, Sprite sp, float expand, float dy, float alpha, Image.Type type)
    {
        var r = NewRect("Shadow", parent);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(-expand, -expand + dy);
        r.offsetMax = new Vector2(expand, expand + dy);
        Img(r, sp, new Color(0, 0, 0, alpha), type);
        return r;
    }

    // Панель: мягкая тень, тёмный контур, градиентное тело, блик сверху, светлая кромка и кольцо.
    // Корень без картинки, чтобы тень была под панелью, а не поверх неё.
    RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                        Color fill, bool shine, float shadowExpand, float shineDelay = 0f)
    {
        var rt = NewRect(name, parent);
        Anchor(rt, anchor, pivot, pos, size);

        ShadowRect(rt, spShadow, shadowExpand, -8f, 0.55f, Image.Type.Sliced);

        var outl = NewRect("Outline", rt);
        Stretch(outl, 0, 0, 0, 0);
        Img(outl, spPanel, Outline, Image.Type.Sliced);

        var body = NewRect("Body", rt);
        Stretch(body, 3, 3, 3, 3);
        Img(body, spPanelGrad, fill, Image.Type.Sliced);

        if (shine)
        {
            var mk = body.gameObject.AddComponent<Mask>();
            mk.showMaskGraphic = true;
            var s = NewRect("Shine", body);
            s.anchorMin = new Vector2(0, 0); s.anchorMax = new Vector2(0, 1);
            s.pivot = new Vector2(0.5f, 0.5f);
            s.sizeDelta = new Vector2(70f, 0f);
            s.anchoredPosition = new Vector2(-200f, 0f);
            Img(s, spShine, new Color(1, 1, 1, 0.3f), Image.Type.Simple);
            var hs = rt.gameObject.AddComponent<HudShine>();
            hs.shine = s;
            hs.delay = shineDelay;
            hs.period = 5.5f;
        }

        var gloss = NewRect("Gloss", rt);
        Stretch(gloss, 3, 3, 3, 3);
        Img(gloss, spPanelGloss, new Color(1, 1, 1, 0.6f), Image.Type.Sliced);

        var rim = NewRect("Rim", rt);
        Stretch(rim, 3, 3, 3, 3);
        Img(rim, spPanelRim, new Color(1, 1, 1, 0.35f), Image.Type.Sliced);

        var ring = NewRect("Ring", rt);
        Stretch(ring, 3, 3, 3, 3);
        Img(ring, spPanelRing, PanelRing, Image.Type.Sliced);
        return rt;
    }

    // Круглый бейдж: тень, контур, градиентная заливка, блик, цветное кольцо, иконка («Badge/Icon» нужен UIManager)
    RectTransform Badge(RectTransform parent, float cx, float size, Sprite icon, float iconSize, Color ringColor)
    {
        var b = NewRect("Badge", parent);
        Anchor(b, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx, 0), new Vector2(size, size));

        float marg = size * 0.2273f;
        var sh = NewRect("Shadow", b);
        sh.anchorMin = Vector2.zero; sh.anchorMax = Vector2.one;
        sh.offsetMin = new Vector2(-marg, -marg - 4f); sh.offsetMax = new Vector2(marg, marg - 4f);
        Img(sh, spRoundShadow, new Color(0, 0, 0, 0.5f), Image.Type.Simple);

        var o = NewRect("Outline", b);
        Stretch(o, 0, 0, 0, 0);
        Img(o, spRoundGrad, Outline, Image.Type.Simple);

        var fill = NewRect("Fill", b);
        Stretch(fill, 3, 3, 3, 3);
        Img(fill, spRoundGrad, BadgeFill, Image.Type.Simple);

        var gloss = NewRect("Gloss", b);
        Stretch(gloss, 3, 3, 3, 3);
        Img(gloss, spRoundGloss, new Color(1, 1, 1, 0.7f), Image.Type.Simple);

        var ring = NewRect("Ring", b);
        Stretch(ring, 3, 3, 3, 3);
        Img(ring, spRoundRing, ringColor, Image.Type.Simple);

        var ic = NewRect("Icon", b);
        Anchor(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
        Img(ic, icon, Color.white, Image.Type.Simple).preserveAspect = true;
        return b;
    }

    // Полоска опыта: обводка, подложка, градиентная заливка, деления и пробегающий блик
    Image Bar(RectTransform parent, float padL, float padR, float padBottom, float h, float fill)
    {
        var outl = NewRect("BarTrack", parent);
        outl.anchorMin = new Vector2(0, 0); outl.anchorMax = new Vector2(1, 0);
        outl.offsetMin = new Vector2(padL, padBottom);
        outl.offsetMax = new Vector2(-padR, padBottom + h);
        Img(outl, spPill, Outline, Image.Type.Sliced);

        var track = NewRect("Track", outl);
        Stretch(track, 2, 2, 2, 2);
        Img(track, spPill, TrackCol, Image.Type.Sliced);

        var clip = NewRect("Clip", track);
        Stretch(clip, 0, 0, 0, 0);
        Img(clip, spPill, Color.white, Image.Type.Sliced);
        var mask = clip.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var f = NewRect("Fill", clip);
        Stretch(f, 0, 0, 0, 0);
        var fi = Img(f, spBar, Green, Image.Type.Filled);
        fi.fillMethod = Image.FillMethod.Horizontal;
        fi.fillOrigin = 0;
        fi.fillAmount = fill;

        for (int i = 1; i <= 3; i++)
        {
            var tk = NewRect("Tick" + i, clip);
            tk.anchorMin = new Vector2(i * 0.25f, 0); tk.anchorMax = new Vector2(i * 0.25f, 1);
            tk.pivot = new Vector2(0.5f, 0.5f);
            tk.sizeDelta = new Vector2(2f, 0f);
            tk.anchoredPosition = Vector2.zero;
            Img(tk, spWhite, new Color(0, 0, 0, 0.3f), Image.Type.Simple);
        }

        var s = NewRect("Shine", clip);
        s.anchorMin = new Vector2(0, 0); s.anchorMax = new Vector2(0, 1);
        s.pivot = new Vector2(0.5f, 0.5f);
        s.sizeDelta = new Vector2(46f, 0f);
        s.anchoredPosition = new Vector2(-100f, 0f);
        Img(s, spShine, new Color(1, 1, 1, 0.55f), Image.Type.Simple);
        var hs = outl.gameObject.AddComponent<HudShine>();
        hs.shine = s;
        hs.period = 3.6f;
        hs.delay = 1.1f;
        hs.sweepFraction = 0.3f;
        return fi;
    }

    TextMeshProUGUI Txt(RectTransform rt, string s, float size, Color c, TextAlignmentOptions align, bool outline)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (outline && outlineMat != null) t.fontSharedMaterial = outlineMat;
        t.text = s;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;
        return t;
    }

    // Материал шрифта с тёмной обводкой (сохраняется как ассет, чтобы не слетал)
    Material MakeOutlineMaterial()
    {
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        if (f == null || f.material == null) return null;
        var m = new Material(f.material) { name = "HUD_TextOutline" };
        m.EnableKeyword("OUTLINE_ON");
        if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0.2f);
        if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", Outline);
        string path = Dir + "/HUD_TextOutline.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Image Img(RectTransform rt, Sprite s, Color c, Image.Type type)
    {
        var i = rt.gameObject.AddComponent<Image>();
        i.sprite = s;
        i.color = c;
        i.type = type;
        i.raycastTarget = false;
        return i;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Anchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static void Stretch(RectTransform r, float left, float top, float right, float bottom)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
    }

    static void AddPop(Component c)
    {
        var t = FindType("HudPop");
        if (t == null)
        {
            Debug.LogWarning("BuilderSim: HudPop.cs не найден - цифры не будут анимироваться.");
            return;
        }
        if (c.GetComponent(t) == null) c.gameObject.AddComponent(t);
    }

    static System.Type FindType(string name)
    {
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    // ================================================================ подключение к UIManager
    static MonoBehaviour FindUiManager()
    {
        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (m != null && m.GetType().Name == "UIManager") return m;
        return null;
    }

    static void Hook(MonoBehaviour ui, Object wood, Object coin, Object level, Object xp, Object objective)
    {
        if (ui == null)
        {
            Debug.LogWarning("BuilderSim: UIManager в сцене не найден, назначь поля вручную.");
            return;
        }
        var so = new SerializedObject(ui);
        Set(so, "woodText", wood);
        Set(so, "coinText", coin);
        Set(so, "levelText", level);
        Set(so, "xpFillBar", xp);
        Set(so, "objectiveText", objective);
        so.ApplyModifiedProperties();
    }

    static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.ObjectReference)
        {
            Debug.LogWarning("UIManager: нет поля " + field + ", назначь вручную.");
            return;
        }
        p.objectReferenceValue = value;
    }

    // ================================================================ спрайты
    delegate Color Glyph(float x, float y, Color c, Color fill);

    void MakeSprites()
    {
        spPanel = MakeSprite("ui_panel", 128, (x, y) => new Color(1, 1, 1, Cov(RRect(x, y, 128, 128, 30))), 34);
        spPanelGrad = MakeSprite("ui_panel_grad", 128, PanelGradPx, 34);
        spPanelRing = MakeSprite("ui_panel_ring", 128, PanelRingPx, 34);
        spPanelRim = MakeSprite("ui_panel_rim", 128, PanelRimPx, 34);
        spPanelGloss = MakeSprite("ui_panel_gloss", 128, PanelGlossPx, 34);
        spShadow = MakeSprite("ui_shadow", 128, ShadowPx, 60);

        spPill = MakeSprite("ui_pill", 32, (x, y) => new Color(1, 1, 1, Cov(RRect(x, y, 32, 32, 16))), 15);
        spWhite = MakeSprite("ui_white", 4, (x, y) => Color.white, 0);
        spBar = MakeSprite("ui_bar", 32, BarPx, 0);
        spShine = MakeSprite("ui_shine", 64, ShinePx, 0);
        spVignette = MakeSprite("ui_vignette", 128, VignettePx, 0);

        spRoundGrad = MakeSprite("ui_round_grad", 128, RoundGradPx, 0);
        spRoundRing = MakeSprite("ui_round_ring", 128, RoundRingPx, 0);
        spRoundGloss = MakeSprite("ui_round_gloss", 128, RoundGlossPx, 0);
        spRoundShadow = MakeSprite("ui_round_shadow", 128, RoundShadowPx, 0);

        spStar = MakeSprite("icon_star", 64, StarPixel, 0);
        if (coinIcon == null) coinIcon = MakeSprite("icon_coin", 64, CoinPixel, 0);
        if (woodIcon == null) woodIcon = MakeSprite("icon_wood", 64, WoodPixel, 0);

        spArrow = MakeSprite("map_arrow", 64, ArrowPx, 0);
        spMapRing = MakeSprite("map_ring", 256, MapRingPx, 0);

        spTent = MakeSprite("poi_tent", 64, (x, y) => PoiPx(x, y, new Color32(0xF2, 0x8C, 0x28, 255), GTent), 0);
        spHut = MakeSprite("poi_hut", 64, (x, y) => PoiPx(x, y, new Color32(0xE0, 0x4F, 0x4F, 255), GHut), 0);
        spMarket = MakeSprite("poi_market", 64, (x, y) => PoiPx(x, y, new Color32(0x3F, 0xA9, 0xF5, 255), GMarket), 0);
        spQuest = MakeSprite("poi_quest", 64, (x, y) => PoiPx(x, y, new Color32(0xF0, 0xB4, 0x1E, 255), GQuest), 0);
        spCamp = MakeSprite("poi_camp", 64, (x, y) => PoiPx(x, y, new Color32(0xFF, 0x6A, 0x2A, 255), GCamp), 0);
        spBridge = MakeSprite("poi_bridge", 64, (x, y) => PoiPx(x, y, new Color32(0x6D, 0x7C, 0xFF, 255), GBridge), 0);
        spForest = MakeSprite("poi_forest", 64, (x, y) => PoiPx(x, y, new Color32(0x3D, 0xB3, 0x5A, 255), GForest), 0);
        spGeneric = MakeSprite("poi_generic", 64, (x, y) => PoiPx(x, y, new Color32(0x7C, 0x8D, 0xB5, 255), GGeneric), 0);
    }

    static Sprite MakeSprite(string name, int size, System.Func<float, float, Color> pixel, int border)
    {
        string path = Dir + "/" + name + ".png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = pixel(x + 0.5f, y + 0.5f);
        tex.SetPixels(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100;
        imp.spriteBorder = new Vector4(border, border, border, border);
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------------------------------------------------------- математика форм
    static float SS(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // расстояние до скруглённого прямоугольника [0,w]x[0,h] (отрицательное внутри)
    static float RRect(float x, float y, float w, float h, float r)
    {
        float cx = x - w * 0.5f, cy = y - h * 0.5f;
        float qx = Mathf.Abs(cx) - (w * 0.5f - r), qy = Mathf.Abs(cy) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }

    static float Circ(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    static float BoxD(float x, float y, float cx, float cy, float hw, float hh, float r)
    {
        return RRect(x - (cx - hw), y - (cy - hh), hw * 2f, hh * 2f, r);
    }

    static float SdTri(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
        Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
        Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
        float d0 = Vector2.Dot(pq0, pq0), d1 = Vector2.Dot(pq1, pq1), d2 = Vector2.Dot(pq2, pq2);
        float c0 = s * (v0.x * e0.y - v0.y * e0.x);
        float c1 = s * (v1.x * e1.y - v1.y * e1.x);
        float c2 = s * (v2.x * e2.y - v2.y * e2.x);
        float dx = Mathf.Min(d0, Mathf.Min(d1, d2));
        float dy = Mathf.Min(c0, Mathf.Min(c1, c2));
        return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
    }

    static float TriD(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        return SdTri(new Vector2(x, y), new Vector2(ax, ay), new Vector2(bx, by), new Vector2(cx, cy));
    }

    // обычное альфа-смешивание «over» поверх under
    static Color Over(Color under, Color over, float cov)
    {
        float a = Mathf.Clamp01(cov) * over.a;
        float oa = a + under.a * (1f - a);
        if (oa <= 0.0001f) return Color.clear;
        float r = (over.r * a + under.r * under.a * (1f - a)) / oa;
        float g = (over.g * a + under.g * under.a * (1f - a)) / oa;
        float b = (over.b * a + under.b * under.a * (1f - a)) / oa;
        return new Color(r, g, b, oa);
    }

    static Color W(Color c, float d) { return Over(c, Color.white, Cov(d)); }

    // ---------------------------------------------------------------- панели
    static Color PanelGradPx(float x, float y)
    {
        float a = Cov(RRect(x, y, 128, 128, 30));
        if (a <= 0f) return Color.clear;
        float g = Mathf.Lerp(0.74f, 1f, y / 128f);
        return new Color(g, g, g, a);
    }

    static Color PanelRingPx(float x, float y)
    {
        float d = RRect(x, y, 128, 128, 30);
        return new Color(1, 1, 1, Mathf.Clamp01(Cov(d) - Cov(d + 4f)));
    }

    static Color PanelRimPx(float x, float y)
    {
        float d = RRect(x, y, 128, 128, 30);
        float a = Mathf.Clamp01(Cov(d) - Cov(d + 3f));
        return new Color(1, 1, 1, a * SS(0.5f, 1f, y / 128f));
    }

    static Color PanelGlossPx(float x, float y)
    {
        float d = RRect(x - 4f, y - 4f, 120, 120, 26);
        return new Color(1, 1, 1, Cov(d) * SS(0.55f, 0.98f, y / 128f) * 0.5f);
    }

    static Color ShadowPx(float x, float y)
    {
        float d = RRect(x - 40f, y - 40f, 48, 48, 20);
        float a = Mathf.Pow(1f - SS(-6f, 38f, d), 1.6f) * 0.9f;
        return new Color(0, 0, 0, a);
    }

    static Color BarPx(float x, float y)
    {
        float t = y / 32f;
        float g = Mathf.Lerp(0.6f, 0.86f, t) + 0.14f * SS(0.58f, 0.86f, t);
        g = Mathf.Min(g, 1f);
        return new Color(g, g, g, 1f);
    }

    static Color ShinePx(float x, float y)
    {
        float u = (x + y) / 128f;
        float a = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) / 0.14f);
        a = a * a * (3f - 2f * a);
        return new Color(1, 1, 1, a);
    }

    static Color VignettePx(float x, float y)
    {
        float dx = (x - 64f) / 64f, dy = (y - 64f) / 64f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return new Color(1, 1, 1, SS(0.55f, 1.2f, d));
    }

    // ---------------------------------------------------------------- круглые элементы
    static Color RoundGradPx(float x, float y)
    {
        float a = Cov(Circ(x, y, 64, 64, 62));
        if (a <= 0f) return Color.clear;
        float g = Mathf.Lerp(0.72f, 1f, y / 128f);
        return new Color(g, g, g, a);
    }

    static Color RoundRingPx(float x, float y)
    {
        float d = Circ(x, y, 64, 64, 62);
        return new Color(1, 1, 1, Mathf.Clamp01(Cov(d) - Cov(d + 5f)));
    }

    static Color RoundGlossPx(float x, float y)
    {
        float d = Circ(x, y, 64, 64, 57);
        return new Color(1, 1, 1, Cov(d) * SS(0.5f, 0.97f, y / 128f) * 0.55f);
    }

    static Color RoundShadowPx(float x, float y)
    {
        float d = Circ(x, y, 64, 64, 44);
        float a = Mathf.Pow(1f - SS(-8f, 20f, d), 1.6f) * 0.9f;
        return new Color(0, 0, 0, a);
    }

    // ---------------------------------------------------------------- иконки ресурсов
    static Color CoinPixel(float x, float y)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
        float a = Cov(d - 30f);
        if (a <= 0f) return Color.clear;
        Color gold = new Color32(0xF5, 0xC5, 0x42, 255);
        Color dark = new Color32(0xC9, 0x9A, 0x1E, 255);
        Color shine = new Color32(0xFF, 0xE9, 0x8A, 255);
        Color c = (d > 25f || (d > 16f && d < 19f)) ? dark : gold;
        if (d < 15f && (x - 32f) + (32f - y) > 10f && d > 5f) c = shine;
        c.a = a;
        return c;
    }

    static Color WoodPixel(float x, float y)
    {
        Color bark = new Color32(0xA9, 0x74, 0x4A, 255);
        Color light = new Color32(0xD9, 0xB0, 0x7A, 255);
        Color ring = new Color32(0xB9, 0x84, 0x55, 255);

        Color c = Color.clear;
        float cb = Cov(RRect(x - 6f, y - 16f, 42f, 32f, 8f));
        if (cb > 0f) c = new Color(bark.r, bark.g, bark.b, cb);

        float dd = Vector2.Distance(new Vector2(x, y), new Vector2(46, 32));
        float cc = Cov(dd - 16f);
        if (cc > 0f)
        {
            Color cap = ((dd > 12.5f && dd < 14.5f) || (dd > 6f && dd < 8f)) ? ring : light;
            c = new Color(Mathf.Lerp(c.r, cap.r, cc), Mathf.Lerp(c.g, cap.g, cc),
                          Mathf.Lerp(c.b, cap.b, cc), Mathf.Max(c.a, cc));
        }
        return c;
    }

    static Color StarPixel(float x, float y)
    {
        float dx = x - 32f, dy = y - 31f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;
        float sec = 2f * Mathf.PI / 5f;
        float t = Mathf.Abs(Mathf.Repeat(a, sec) - sec * 0.5f) / (sec * 0.5f);
        float r = Mathf.Lerp(13f, 29f, t);
        float cov = Cov(d - r);
        if (cov <= 0f) return Color.clear;
        Color gold = new Color32(0xFF, 0xD3, 0x4D, 255);
        Color dark = new Color32(0xD9, 0x9A, 0x1A, 255);
        Color c = (d > r - 3.5f) ? dark : gold;
        c.a = cov;
        return c;
    }

    // ---------------------------------------------------------------- карта: стрелка, рамка, метки
    static Color ArrowPx(float x, float y)
    {
        Vector2 p = new Vector2(x, y);
        float d1 = SdTri(p, new Vector2(32, 60), new Vector2(12, 8), new Vector2(32, 22));
        float d2 = SdTri(p, new Vector2(32, 60), new Vector2(32, 22), new Vector2(52, 8));
        float d = Mathf.Min(d1, d2);
        Color c = Over(Color.clear, new Color32(0x0E, 0x14, 0x26, 255), Cov(d - 4.5f));
        c = Over(c, Color.white, Cov(d - 2.8f));
        c = Over(c, new Color32(0x8F, 0xD8, 0xFF, 255), Cov(d1));
        c = Over(c, new Color32(0x3F, 0x86, 0xF0, 255), Cov(d2));
        return c;
    }

    static Color MapRingPx(float x, float y)
    {
        float dx = x - 128f, dy = y - 128f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Cov(d - 127f) * Cov(108f - d);
        if (a <= 0f) return Color.clear;

        float t = y / 256f;
        Color dark = new Color32(0x14, 0x1C, 0x34, 255);
        Color c = Color.Lerp(new Color32(0x4A, 0x5C, 0x8E, 255), new Color32(0xCB, 0xD8, 0xF4, 255), t);
        float edge = Mathf.Max(SS(113f, 108f, d), SS(122f, 127f, d));
        c = Color.Lerp(c, dark, edge * 0.75f);

        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        float m = Mathf.Repeat(ang, 30f);
        float dt = Mathf.Min(m, 30f - m);
        float px = dt * Mathf.Deg2Rad * d;
        if (px < 1.3f && d > 112f && d < 122f) c = Color.Lerp(c, dark, 0.6f);

        c.a = a;
        return c;
    }

    // круглая метка: тёмный контур, градиентная заливка цвета типа, светлая кромка, белый глиф
    static Color PoiPx(float x, float y, Color fill, Glyph glyph)
    {
        float t = y / 64f;
        Color c = Over(Color.clear, new Color32(0x0E, 0x14, 0x26, 255), Cov(Circ(x, y, 32, 32, 30.5f)));
        Color f = Color.Lerp(fill * 0.7f, fill, t);
        f.a = 1f;
        c = Over(c, f, Cov(Circ(x, y, 32, 32, 27f)));
        float dr = Mathf.Abs(Circ(x, y, 32, 32, 25.5f)) - 1f;
        c = Over(c, new Color(1, 1, 1, 0.4f * SS(0.5f, 1f, t)), Cov(dr));
        return glyph(x, y, c, f);
    }

    static Color GTent(float x, float y, Color c, Color fill)
    {
        c = W(c, TriD(x, y, 32, 50, 13, 17, 51, 17));
        c = Over(c, fill, Cov(TriD(x, y, 32, 34, 26.5f, 17, 37.5f, 17)));
        c = W(c, BoxD(x, y, 32, 16, 21, 1.6f, 0.8f));
        return c;
    }

    static Color GHut(float x, float y, Color c, Color fill)
    {
        c = W(c, BoxD(x, y, 32, 26, 13, 9, 1.5f));
        c = W(c, TriD(x, y, 32, 49, 12, 33, 52, 33));
        c = W(c, BoxD(x, y, 41, 44, 3, 5, 0.5f));
        c = Over(c, fill, Cov(BoxD(x, y, 32, 22, 3.5f, 5, 1f)));
        return c;
    }

    static Color GMarket(float x, float y, Color c, Color fill)
    {
        c = W(c, BoxD(x, y, 32, 41, 18, 5, 1.5f));
        for (int k = -1; k <= 1; k++) c = W(c, Circ(x, y, 32 + k * 12, 36, 6.2f));
        c = Over(c, fill, Cov(BoxD(x, y, 26, 41, 1.2f, 5, 0.3f)));
        c = Over(c, fill, Cov(BoxD(x, y, 38, 41, 1.2f, 5, 0.3f)));
        c = W(c, BoxD(x, y, 32, 22, 16, 4.5f, 1f));
        c = W(c, BoxD(x, y, 18.5f, 28, 1.6f, 6, 0.5f));
        c = W(c, BoxD(x, y, 45.5f, 28, 1.6f, 6, 0.5f));
        return c;
    }

    static Color GQuest(float x, float y, Color c, Color fill)
    {
        c = W(c, BoxD(x, y, 32, 37, 4.2f, 10.5f, 3.5f));
        c = W(c, Circ(x, y, 32, 19, 4.6f));
        return c;
    }

    static Color GCamp(float x, float y, Color c, Color fill)
    {
        c = W(c, Circ(x, y, 32, 27, 10f));
        c = W(c, TriD(x, y, 32, 50, 22.5f, 29, 41.5f, 29));
        c = Over(c, fill, Cov(Circ(x, y, 32, 25, 4.8f)));
        c = Over(c, fill, Cov(TriD(x, y, 32, 36, 27, 26, 37, 26)));
        c = W(c, BoxD(x, y, 32, 14, 15, 2.4f, 1f));
        return c;
    }

    static Color GBridge(float x, float y, Color c, Color fill)
    {
        float arch = Mathf.Max(Mathf.Abs(Circ(x, y, 32, 27, 13f)) - 2.4f, -(y - 27f));
        c = W(c, arch);
        c = W(c, BoxD(x, y, 32, 27, 21, 2.8f, 1f));
        c = W(c, BoxD(x, y, 13.5f, 21, 2, 6, 0.6f));
        c = W(c, BoxD(x, y, 50.5f, 21, 2, 6, 0.6f));
        return c;
    }

    static Color GForest(float x, float y, Color c, Color fill)
    {
        c = W(c, Circ(x, y, 24.5f, 37, 9f));
        c = W(c, Circ(x, y, 39.5f, 37, 9f));
        c = W(c, Circ(x, y, 32, 45, 9.5f));
        c = W(c, BoxD(x, y, 32, 20, 3.2f, 8, 0.8f));
        return c;
    }

    static Color GGeneric(float x, float y, Color c, Color fill)
    {
        c = W(c, Circ(x, y, 32, 32, 8f));
        c = Over(c, fill, Cov(Circ(x, y, 32, 32, 3.5f)));
        return c;
    }

}
#endif