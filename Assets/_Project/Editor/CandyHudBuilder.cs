#if UNITY_EDITOR
// Assets/_Project/Editor/UI/Candy/CandyHudBuilder.cs   (ДОЛЖЕН лежать в папке Editor)
//
// Сборщик нового HUD «Candy» из спрайтов Candy Kit (их делает CandyUiKitGenerator).
// Меню:
//   Tools > BuilderSim > UI > Candy Kit > Собрать HUD (ресурсы, задание, уровень)
//   Tools > BuilderSim > UI > Candy Kit > Вернуть старый HUD
//
// Что собирается В СЦЕНЕ (обычные объекты, в игре ничего не строится), всё под корнем «CandyHud» в HUD-канвасе:
//   CandyResources   три плашки Coins / Wood / Planks (panel_chip, иконка выступает за левый край, число справа).
//                    HudGainAnchor на монетах и дереве («+N» и полёт брёвен), HudPlanks на досках, HudPop на числах.
//   Level            звезда, плашка «LVL N» (lvl_plate), полоса опыта (xp_frame + xp_fill, Image.Type.Filled).
//   CandyTask        компонент CandyTaskView + панель «Task» (panel_quest), деревянная табличка «ЗАДАНИЕ» (sign_wood),
//                    заголовок, подсказка, кнопка-шеврон.
// Подключается:
//   UIManager.coinText / woodText / levelText / xpFillBar  -> новые тексты и полоса (UIManager по-прежнему пишет в них сам).
//   UIManager.objectiveText                                -> НЕ меняется: остаётся прежним (скрытым) текстом, его читают QuestGuide и др.;
//                                                            CandyTaskView сам пишет в него простой текст цели.
//   CandyHud.old*  / hiddenOld                             -> что было раньше, чтобы «Вернуть старый HUD» всё восстановил.
//
// Старые панели ресурсов, уровня и задания ОТКЛЮЧАЮТСЯ (SetActive(false)), не удаляются.
// Повторный запуск безопасен: прежний CandyHud сначала откатывается, потом собирается заново.
//
// ВАЖНО: нужны две маленькие правки в старых скриптах (без них будет «не хватать»):
//   1) HudKitSkin.Apply(): не вызывать SkinLevel(ui) и SkinTask(ui), пока CandyHud.Active (иначе старый скин перекрасит новые панели).
//   2) UIManager.SetObjective / UpdateObjective: если CandyTaskView.Instance != null, отдавать цель ему, а не ObjectiveView.
// Точный код правок смотри в сообщении, с которым пришёл этот файл.
//
// Нужные спрайты: сначала запусти Tools > BuilderSim > UI > Candy Kit > Сгенерировать спрайты и лист.
// Две иконки, которых в наборе пока нет (галочка и шеврон), сборщик дорисовывает сам и кладёт рядом:
//   Icons/icon_check.png, Extra/icon_chevron.png  (в Candy Kit v2 они уже есть, поэтому здесь не рисуются; запасная отрисовка осталась на случай удаления файлов).
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CandyHudBuilder
{
    // ---------------------------------------------------------------- настройки (единицы канваса 1920x1080)
    const string Dir = CandyUiKitGenerator.OutDir;

    const string RootName = "CandyHud";
    const string ResName = "CandyResources";
    const string LevelName = "Level";          // имена Level и Task нужны HudSafeAreaRegistry (ищет панели по имени)
    const string TaskRootName = "CandyTask";
    const string TaskPanelName = "Task";

    // ресурсы
    const float PillW = 176f, PillH = 60f, PillGap = 14f;
    const float IconSize = 60f, IconCenterX = 18f;     // иконка выступает за левый край плашки на 12
    const float ValueLeft = 52f, ValueRight = 16f;
    const float NumberMin = 22f, NumberMax = 34f;
    static readonly Vector2 DefaultResPos = new Vector2(46f, -40f);

    // уровень
    static readonly Vector2 LevelSize = new Vector2(344f, 88f);
    static readonly Vector2 DefaultLevelPos = new Vector2(-40f, -36f);

    // задание
    const float TaskWidth = 560f;
    static readonly Vector2 DefaultTaskPos = new Vector2(0f, -34f);

    static readonly Color WoodText = new Color32(0x3A, 0x1F, 0x0B, 255);

    static readonly string[] IconKeys = { "coin", "log", "planks", "axe", "boot", "backpack", "saw", "star", "trophy", "paw", "gift", "lock" };

    sealed class Kit
    {
        public Sprite chip, quest, sign, barFrame, barBlue, barMint, xpFrame, xpFill, lvlPlate, check, chevron;
        public readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    }

    // ================================================================ меню
    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Собрать HUD (ресурсы, задание, уровень)", false, 210)]
    static void MenuBuild() { Build(); }

    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Вернуть старый HUD", false, 211)]
    static void MenuRevert()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Info("Останови Play и запусти ещё раз: в режиме Play изменения сцены не сохраняются."); return; }
        var ch = FindOne<CandyHud>();
        if (ch == null) { Info("В сцене нет объекта CandyHud: возвращать нечего."); return; }
        var ui = FindOne<UIManager>();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Candy HUD: вернуть старый");
        int g = Undo.GetCurrentGroup();
        var scene = ch.gameObject.scene;
        Revert(ch, ui);
        Undo.CollapseUndoOperations(g);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[CandyHud] Старый HUD возвращён, CandyHud удалён. Сохрани сцену (Ctrl+S).");
    }

    // ================================================================ сборка
    static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Info("Останови Play и запусти ещё раз: в режиме Play изменения сцены не сохраняются."); return; }

        var ui = FindOne<UIManager>();
        if (ui == null) { Info("В сцене нет UIManager. Открой сцену с HUD и запусти ещё раз."); return; }

        // ---- 1. спрайты (до любых изменений сцены, чтобы при ошибке ничего не сломать)
        List<string> missing;
        var kit = LoadKit(out missing);
        if (kit == null)
        {
            Info("Не нашёл спрайты Candy Kit в " + Dir + ":\n\n" + string.Join("\n", missing) +
                 "\n\nСначала запусти Tools > BuilderSim > UI > Candy Kit > Сгенерировать спрайты и лист, потом повтори.");
            return;
        }

        // ---- 2. прежний CandyHud
        var existing = FindOne<CandyHud>();
        if (existing != null && !EditorUtility.DisplayDialog("Candy HUD", "CandyHud уже есть в сцене. Пересобрать его заново?\n\nСтарый HUD при этом вернётся и скроется опять.", "Пересобрать", "Отмена"))
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Candy HUD: собрать");
        int undoGroup = Undo.GetCurrentGroup();

        if (existing != null) Revert(existing, ui);

        // ---- 3. что есть сейчас (после отката это снова старые элементы)
        var oldRes = FindOldResources(ui);
        var oldLevel = ui.levelText != null ? ui.levelText.transform.parent as RectTransform : null;
        var oldTask = ui.objectiveText != null ? ui.objectiveText.transform.parent as RectTransform : null;
        if (oldLevel != null && !CanHide(oldLevel, ui.coinText, ui.woodText, ui.objectiveText)) oldLevel = null;
        if (oldTask != null && !CanHide(oldTask, ui.coinText, ui.woodText, ui.levelText, ui.xpFillBar)) oldTask = null;

        Transform hud = null;
        if (oldRes.Count > 0) hud = oldRes[0].transform.parent;
        if (hud == null && oldLevel != null) hud = oldLevel.parent;
        if (hud == null && oldTask != null) hud = oldTask.parent;
        if (hud == null)
        {
            Canvas c = ui.coinText != null ? ui.coinText.GetComponentInParent<Canvas>(true) : null;
            if (c == null && ui.levelText != null) c = ui.levelText.GetComponentInParent<Canvas>(true);
            var auto = GameObject.Find("HUD_Auto");
            hud = c != null ? c.rootCanvas.transform : (auto != null ? auto.transform : null);
        }
        if (hud == null) { Info("Не нашёл HUD-канвас (HUD_Auto). Сначала собери HUD: Tools > BuilderSim > Build HUD."); Undo.CollapseUndoOperations(undoGroup); return; }

        TMP_FontAsset font = ui.objectiveText != null ? ui.objectiveText.font : null;
        if (font == null && ui.coinText != null) font = ui.coinText.font;
        if (font == null) Debug.LogWarning("[CandyHud] Не нашёл шрифт в UIManager.objectiveText: тексты получат шрифт TMP по умолчанию (кириллицы может не быть).");

        int layer = hud.gameObject.layer;
        int sibling = oldRes.Count > 0 ? oldRes[0].transform.GetSiblingIndex() : hud.childCount;

        // позиции старых панелей (если они лежат прямо в том же HUD-корне)
        RectTransform oldResRt = oldRes.Count > 0 ? oldRes[0].transform as RectTransform : null;

        // ---- 4. корень CandyHud
        var rootGo = new GameObject(RootName, typeof(RectTransform));
        rootGo.layer = layer;
        Undo.RegisterCreatedObjectUndo(rootGo, "Candy HUD: собрать");
        var root = (RectTransform)rootGo.transform;
        root.SetParent(hud, false);
        root.SetSiblingIndex(Mathf.Min(sibling, hud.childCount - 1));
        Stretch(root);
        var ch = rootGo.AddComponent<CandyHud>();

        // ---- 5. ресурсы
        TMP_Text coinValue, woodValue, plankValue;
        var resRoot = BuildResources(root, kit, font, layer, oldResRt, hud, out coinValue, out woodValue, out plankValue);

        // ---- 6. уровень
        TMP_Text levelValue;
        Image xpFill;
        BuildLevel(root, kit, font, layer, oldLevel, hud, out levelValue, out xpFill);

        // ---- 7. задание
        var taskView = BuildTask(root, kit, font, layer, oldTask, hud);

        // ---- 8. CandyHud: словарь спрайтов, ссылки на старое
        ch.sprites = new List<CandyHud.Entry>();
        foreach (var kv in kit.icons) ch.sprites.Add(new CandyHud.Entry { key = "icon_" + kv.Key, sprite = kv.Value });
        ch.sprites.Add(new CandyHud.Entry { key = "icon_check", sprite = kit.check });
        ch.sprites.Add(new CandyHud.Entry { key = "icon_chevron", sprite = kit.chevron });

        ch.oldCoinText = ui.coinText;
        ch.oldWoodText = ui.woodText;
        ch.oldLevelText = ui.levelText;
        ch.oldObjectiveText = ui.objectiveText;
        ch.oldXpFill = ui.xpFillBar;
        ch.hiddenOld = new List<GameObject>();

        // ---- 9. UIManager
        Undo.RecordObject(ui, "Candy HUD: собрать");
        ui.coinText = coinValue;
        ui.woodText = woodValue;
        ui.levelText = levelValue;
        ui.xpFillBar = xpFill;
        EditorUtility.SetDirty(ui);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui);

        // ---- 10. прячем старое
        foreach (var go in oldRes) Hide(go, ch);
        if (oldLevel != null) Hide(oldLevel.gameObject, ch);
        if (oldTask != null) Hide(oldTask.gameObject, ch);

        EditorUtility.SetDirty(ch);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = rootGo;
        EditorSceneManager.MarkSceneDirty(rootGo.scene);

        string warn = "";
        if (oldLevel == null) warn += " Старая панель уровня не найдена или общая с другими элементами: не скрыта.";
        if (oldTask == null) warn += " Старая панель задания не найдена или общая с другими элементами: не скрыта.";
        Debug.Log("[CandyHud] Готово: ресурсы, уровень и задание собраны. Скрыто старых панелей: " + ch.hiddenOld.Count + "." + warn +
                  "\nДальше: 1) правка HudKitSkin и UIManager (см. шапку файла), 2) Ctrl+S, 3) Play.", rootGo);
    }

    // ================================================================ ресурсы
    static RectTransform BuildResources(RectTransform parent, Kit kit, TMP_FontAsset font, int layer, RectTransform oldRoot, Transform hud,
                                        out TMP_Text coinValue, out TMP_Text woodValue, out TMP_Text plankValue)
    {
        var go = new GameObject(ResName, typeof(RectTransform), typeof(CanvasGroup));
        go.layer = layer;
        var root = (RectTransform)go.transform;
        root.SetParent(parent, false);
        float baseW = PillW * 2f + PillGap;
        if (oldRoot != null && oldRoot.parent == hud)
        {
            root.anchorMin = oldRoot.anchorMin; root.anchorMax = oldRoot.anchorMax; root.pivot = oldRoot.pivot;
            root.anchoredPosition = oldRoot.anchoredPosition;
        }
        else
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = DefaultResPos;
        }
        root.sizeDelta = new Vector2(baseW, PillH);
        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;        // HUD не перехватывает клики
        cg.interactable = false;
        go.AddComponent<HudSafeAreaElement>();

        RectTransform coinIcon, woodIcon, plankIcon, coinFrame, woodFrame, plankFrame;
        var coinSlot = Pill(root, "Coins", 0, kit.chip, kit.icons["coin"], font, layer, out coinValue, out coinIcon, out coinFrame);
        var woodSlot = Pill(root, "Wood", 1, kit.chip, kit.icons["log"], font, layer, out woodValue, out woodIcon, out woodFrame);
        var plankSlot = Pill(root, "Planks", 2, kit.chip, kit.icons["planks"], font, layer, out plankValue, out plankIcon, out plankFrame);

        // «+N» и полёт брёвен
        Anchor(coinSlot, HudGain.Kind.Coin, coinIcon, coinValue, coinFrame);
        Anchor(woodSlot, HudGain.Kind.Wood, woodIcon, woodValue, woodFrame);

        // иконки «дышат»
        Shine(coinSlot, coinIcon, 0f);
        Shine(woodSlot, woodIcon, 0.8f);
        Shine(plankSlot, plankIcon, 1.6f);

        // слот досок: плавное появление
        var planks = plankSlot.gameObject.AddComponent<HudPlanks>();
        planks.group = plankSlot.gameObject.AddComponent<CanvasGroup>();
        planks.value = plankValue;
        planks.icon = plankIcon;
        planks.panel = root;
        planks.divider = null;
        planks.baseWidth = baseW;
        planks.slotWidth = PillW + PillGap;
        return root;
    }

    static RectTransform Pill(RectTransform root, string name, int index, Sprite chip, Sprite icon, TMP_FontAsset font, int layer,
                              out TMP_Text value, out RectTransform iconRt, out RectTransform frameRt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        var slot = (RectTransform)go.transform;
        slot.SetParent(root, false);
        slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
        slot.pivot = new Vector2(0.5f, 0.5f);                       // «подпрыгивание» (HudGainAnchor.Punch) идёт от центра плашки
        slot.sizeDelta = new Vector2(PillW, PillH);
        slot.anchoredPosition = new Vector2(PillW * 0.5f + index * (PillW + PillGap), -PillH * 0.5f);

        var frame = Img(slot, "Frame", chip, true, layer);
        Stretch(frame.rectTransform);
        frameRt = frame.rectTransform;

        var ic = Img(slot, "Icon", icon, false, layer);
        ic.preserveAspect = true;
        var icr = ic.rectTransform;
        icr.anchorMin = icr.anchorMax = new Vector2(0f, 0.5f);
        icr.pivot = new Vector2(0.5f, 0.5f);
        icr.sizeDelta = new Vector2(IconSize, IconSize);
        icr.anchoredPosition = new Vector2(IconCenterX, 0f);
        iconRt = icr;

        var t = Txt(slot, "Value", "0", NumberMax, CandyTaskView.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, font, layer);
        var vr = t.rectTransform;
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.pivot = new Vector2(0f, 0.5f);                            // HudPop раздувает число от левого края
        vr.offsetMin = new Vector2(ValueLeft, 0f);
        vr.offsetMax = new Vector2(-ValueRight, 0f);
        t.enableAutoSizing = true;
        t.fontSizeMin = NumberMin;
        t.fontSizeMax = NumberMax;
        t.gameObject.AddComponent<HudPop>();
        value = t;
        return slot;
    }

    static void Anchor(RectTransform slot, HudGain.Kind kind, RectTransform icon, TMP_Text value, RectTransform frame)
    {
        var a = slot.gameObject.AddComponent<HudGainAnchor>();
        a.kind = kind;
        a.flyTarget = icon;        // бревна и иконка цели летят в иконку плашки
        a.font = value.font;       // шрифт плашки «+N»
        a.gainBehind = frame;      // плашка выезжает из-под фона
    }

    static void Shine(RectTransform slot, RectTransform icon, float delay)
    {
        var hs = slot.gameObject.AddComponent<HudShine>();
        hs.breathe = icon;
        hs.delay = delay;
        hs.period = 5.5f;
        hs.shine = null;           // бегущего блика у плоского стиля нет
    }

    // ================================================================ уровень
    static void BuildLevel(RectTransform parent, Kit kit, TMP_FontAsset font, int layer, RectTransform oldLevel, Transform hud,
                           out TMP_Text levelValue, out Image xpFill)
    {
        var go = new GameObject(LevelName, typeof(RectTransform));
        go.layer = layer;
        var root = (RectTransform)go.transform;
        root.SetParent(parent, false);
        if (oldLevel != null && oldLevel.parent == hud)
        {
            root.anchorMin = oldLevel.anchorMin; root.anchorMax = oldLevel.anchorMax; root.pivot = oldLevel.pivot;
            root.anchoredPosition = oldLevel.anchoredPosition;
        }
        else
        {
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = DefaultLevelPos;
        }
        root.sizeDelta = LevelSize;

        // звезда слева
        var star = Img(root, "Star", kit.icons["star"], false, layer);
        star.preserveAspect = true;
        var sr = star.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0f, 0.5f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.sizeDelta = new Vector2(84f, 84f);
        sr.anchoredPosition = new Vector2(42f, 0f);

        // плашка «LVL N»
        var plate = Img(root, "Plate", kit.lvlPlate, true, layer);
        var pr = plate.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0f, 1f);
        pr.pivot = new Vector2(0f, 1f);
        pr.sizeDelta = new Vector2(112f, 40f);
        pr.anchoredPosition = new Vector2(92f, -2f);

        var lv = Txt(pr, "LevelText", "LVL 1", 26f, CandyTaskView.Ink, TextAlignmentOptions.Center, FontStyles.Bold, font, layer);
        var lr = lv.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(8f, 2f); lr.offsetMax = new Vector2(-8f, -2f);
        lv.enableAutoSizing = true;
        lv.fontSizeMin = 18f; lv.fontSizeMax = 28f;
        levelValue = lv;

        // полоса опыта
        var bar = Img(root, "XpBar", kit.xpFrame, true, layer);
        var br = bar.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0f, 0f);
        br.pivot = new Vector2(0f, 0f);
        br.sizeDelta = new Vector2(248f, 34f);
        br.anchoredPosition = new Vector2(92f, 6f);

        var fill = Img(br, "XpFill", kit.xpFill, false, layer);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;                 // UIManager двигает fillAmount
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        xpFill = fill;
    }

    // ================================================================ задание
    static CandyTaskView BuildTask(RectTransform parent, Kit kit, TMP_FontAsset font, int layer, RectTransform oldTask, Transform hud)
    {
        var rgo = new GameObject(TaskRootName, typeof(RectTransform));
        rgo.layer = layer;
        var troot = (RectTransform)rgo.transform;
        troot.SetParent(parent, false);
        Stretch(troot);                                  // всегда включённый родитель панели
        var view = rgo.AddComponent<CandyTaskView>();

        var pgo = new GameObject(TaskPanelName, typeof(RectTransform), typeof(Image));
        pgo.layer = layer;
        var panel = (RectTransform)pgo.transform;
        panel.SetParent(troot, false);
        if (oldTask != null && oldTask.parent == hud)
        {
            panel.anchorMin = oldTask.anchorMin; panel.anchorMax = oldTask.anchorMax; panel.pivot = oldTask.pivot;
            panel.anchoredPosition = oldTask.anchoredPosition;
        }
        else
        {
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = DefaultTaskPos;
        }
        panel.sizeDelta = new Vector2(TaskWidth, 170f);
        var pim = pgo.GetComponent<Image>();
        pim.sprite = kit.quest;
        pim.type = Image.Type.Sliced;
        pim.raycastTarget = false;

        // табличка «ЗАДАНИЕ» на двух верёвках: нижний край доски заходит на панель
        var sign = Img(panel, "Sign", kit.sign, true, layer);
        var sr = sign.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.sizeDelta = new Vector2(250f, 84f);
        sr.anchoredPosition = new Vector2(0f, -16f);
        var st = Txt(sr, "Label", "ЗАДАНИЕ", 28f, WoodText, TextAlignmentOptions.Center, FontStyles.Bold, font, layer);
        var strt = st.rectTransform;
        strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one;
        strt.offsetMin = new Vector2(24f, 10f); strt.offsetMax = new Vector2(-24f, -22f);
        st.enableAutoSizing = true;
        st.fontSizeMin = 20f; st.fontSizeMax = 30f;

        // заголовок и подсказка (позиции в игре выставляет CandyTaskView.Layout; здесь просто заготовка)
        var title = Txt(panel, "Title", "Сделай досок для Миры", 28f, CandyTaskView.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold, font, layer);
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
        tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(34f, -64f);
        tr.sizeDelta = new Vector2(TaskWidth - 68f, 36f);
        title.enableWordWrapping = true;

        var footer = Txt(panel, "Footer", "", 22f, CandyTaskView.Muted, TextAlignmentOptions.TopLeft, FontStyles.Normal, font, layer);
        var fr = footer.rectTransform;
        fr.anchorMin = fr.anchorMax = new Vector2(0f, 1f);
        fr.pivot = new Vector2(0f, 1f);
        fr.anchoredPosition = new Vector2(34f, -110f);
        fr.sizeDelta = new Vector2(TaskWidth - 68f, 28f);
        footer.enableWordWrapping = true;
        footer.gameObject.SetActive(false);

        // кнопка-шеврон: сворачивание
        var cgo = new GameObject("Chevron", typeof(RectTransform), typeof(Image), typeof(Button));
        cgo.layer = layer;
        var crt = (RectTransform)cgo.transform;
        crt.SetParent(panel, false);
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.sizeDelta = new Vector2(48f, 48f);
        crt.anchoredPosition = new Vector2(-14f, -14f);
        var cim = cgo.GetComponent<Image>();
        cim.sprite = kit.chevron;
        cim.preserveAspect = true;
        cim.raycastTarget = true;
        var btn = cgo.GetComponent<Button>();
        btn.targetGraphic = cim;
        btn.transition = Selectable.Transition.ColorTint;
        var cols = btn.colors;
        cols.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        cols.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cols.selectedColor = Color.white;
        btn.colors = cols;
        var nav = new Navigation(); nav.mode = Navigation.Mode.None; btn.navigation = nav;

        view.panel = panel;
        view.title = title;
        view.footer = footer;
        view.chevron = btn;
        view.chevronRt = crt;
        view.barFrame = kit.barFrame;
        view.barFillBlue = kit.barBlue;
        view.barFillMint = kit.barMint;
        view.width = TaskWidth;
        return view;
    }

    // ================================================================ откат
    static void Revert(CandyHud ch, UIManager ui)
    {
        if (ui != null)
        {
            Undo.RecordObject(ui, "Candy HUD: вернуть старый");
            if (ch.oldCoinText != null) ui.coinText = ch.oldCoinText;
            if (ch.oldWoodText != null) ui.woodText = ch.oldWoodText;
            if (ch.oldLevelText != null) ui.levelText = ch.oldLevelText;
            if (ch.oldXpFill != null) ui.xpFillBar = ch.oldXpFill;
            if (ch.oldObjectiveText != null) ui.objectiveText = ch.oldObjectiveText;
            EditorUtility.SetDirty(ui);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
        }
        if (ch.hiddenOld != null)
        {
            foreach (var go in ch.hiddenOld)
            {
                if (go == null) continue;
                Undo.RecordObject(go, "Candy HUD: вернуть старый");
                go.SetActive(true);
                EditorUtility.SetDirty(go);
            }
        }
        Undo.DestroyObjectImmediate(ch.gameObject);
    }

    static void Hide(GameObject go, CandyHud ch)
    {
        if (go == null || !go.activeSelf) return;
        Undo.RecordObject(go, "Candy HUD: собрать");
        go.SetActive(false);
        ch.hiddenOld.Add(go);
        EditorUtility.SetDirty(go);
    }

    // ================================================================ поиск старого
    // старая панель ресурсов = «ResourcePanel» (Build Resource Panel) или родители текстов монет и дерева (BuilderSimHud)
    static List<GameObject> FindOldResources(UIManager ui)
    {
        var list = new List<GameObject>();
        var p = ui.coinText != null ? ui.coinText.transform.parent : null;
        if (p != null && p.parent != null && p.parent.name == "ResourcePanel") { list.Add(p.parent.gameObject); return list; }
        foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rt != null && rt.name == "ResourcePanel" && rt.gameObject.scene.IsValid()) { list.Add(rt.gameObject); return list; }
        AddOld(list, ui.coinText, ui);
        AddOld(list, ui.woodText, ui);
        return list;
    }

    static void AddOld(List<GameObject> list, TMP_Text text, UIManager ui)
    {
        if (text == null) return;
        var p = text.transform.parent;
        if (p == null || list.Contains(p.gameObject)) return;
        if (p.GetComponent<Canvas>() != null) return;                                  // текст лежит прямо на канвасе: панели нет
        if (!CanHide(p, ui.levelText, ui.objectiveText, ui.xpFillBar)) return;         // в панели лежит что-то ещё важное
        list.Add(p.gameObject);
    }

    // панель можно скрыть, только если в ней не лежат важные чужие элементы
    static bool CanHide(Transform panel, params Component[] mustNotHold)
    {
        if (panel == null || panel.GetComponent<Canvas>() != null) return false;
        foreach (var c in mustNotHold)
            if (c != null && (c.transform == panel || c.transform.IsChildOf(panel))) return false;
        return true;
    }

    // ================================================================ спрайты
    static Kit LoadKit(out List<string> missing)
    {
        missing = new List<string>();
        var k = new Kit();
        k.chip = Need("Panels/panel_chip", missing);
        k.quest = Need("Panels/panel_quest", missing);
        k.sign = Need("Panels/sign_wood", missing);
        k.barFrame = Need("Controls/bar_frame", missing);
        k.barBlue = Need("Controls/bar_fill_blue", missing);
        k.barMint = Need("Controls/bar_fill_mint", missing);
        k.xpFrame = Need("Controls/xp_frame", missing);
        k.xpFill = Need("Controls/xp_fill", missing);
        k.lvlPlate = Need("Controls/lvl_plate", missing);
        foreach (var key in IconKeys) k.icons[key] = Need("Icons/icon_" + key, missing);
        if (missing.Count > 0) return null;

        // галочку и шеврон набор пока не делает: дорисовываем и кладём рядом (существующие файлы не трогаем)
        k.check = EnsureGenerated("Icons/icon_check", DrawCheck);
        k.chevron = EnsureGenerated("Extra/icon_chevron", DrawChevron);
        if (k.check == null || k.chevron == null)
        {
            missing.Add("Icons/icon_check.png или Extra/icon_chevron.png не удалось создать (см. Console)");
            return null;
        }
        return k;
    }

    static Sprite Need(string rel, List<string> missing)
    {
        string path = Dir + "/" + rel + ".png";
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) missing.Add(path);
        return s;
    }

    delegate void DrawFn(Color32[] px, int n);

    static Sprite EnsureGenerated(string rel, DrawFn draw)
    {
        string path = Dir + "/" + rel + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        try
        {
            const int n = 128;
            var px = new Color32[n * n];
            draw(px, n);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 200f;                      // как у остального Candy Kit (Q = 2)
            imp.spriteBorder = Vector4.zero;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.isReadable = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    // ---- мини-растеризатор для двух недостающих иконок (y вверх, координаты 0..128)
    static readonly Color NavyC = new Color32(0x2B, 0x2F, 0x5C, 255);

    static void DrawCheck(Color32[] px, int n)
    {
        var pts = new[] { new Vector2(28f, 68f), new Vector2(54f, 42f), new Vector2(102f, 94f) };
        PaintPolyline(px, n, pts, 17f, NavyC);                                        // контур
        PaintPolyline(px, n, pts, 10.5f, new Color32(0x4F, 0xC7, 0x9A, 255));          // мятная заливка
        PaintPolyline(px, n, new[] { new Vector2(34f, 72f), new Vector2(54f, 52f) }, 3.2f, new Color(1f, 1f, 1f, 0.55f));   // блик
    }

    static void DrawChevron(Color32[] px, int n)
    {
        PaintDisc(px, n, 64f, 62f, 60f, NavyC);                                       // контур
        PaintDisc(px, n, 64f, 62f, 54f, new Color32(0x3A, 0x82, 0xD2, 255));            // тёмная подложка
        PaintDisc(px, n, 64f, 66f, 49f, new Color32(0x69, 0xB4, 0xF2, 255));            // светлая часть
        var pts = new[] { new Vector2(54f, 40f), new Vector2(78f, 64f), new Vector2(54f, 88f) };   // «>» как в Candy Kit v2; CandyHud сам поворачивает её на ±90°
        PaintPolyline(px, n, pts, 12f, NavyC);
        PaintPolyline(px, n, pts, 8f, Color.white);
    }

    static void PaintDisc(Color32[] px, int n, float cx, float cy, float r, Color c)
    {
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                Blend(px, y * n + x, c, Mathf.Clamp01(r + 0.5f - d));
            }
    }

    static void PaintPolyline(Color32[] px, int n, Vector2[] pts, float halfW, Color c)
    {
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float best = float.MaxValue;
                for (int i = 0; i + 1 < pts.Length; i++) best = Mathf.Min(best, SegDist(x + 0.5f, y + 0.5f, pts[i], pts[i + 1]));
                Blend(px, y * n + x, c, Mathf.Clamp01(halfW + 0.5f - best));
            }
    }

    static float SegDist(float px, float py, Vector2 a, Vector2 b)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float t = Mathf.Clamp01(((px - a.x) * dx + (py - a.y) * dy) / (dx * dx + dy * dy));
        float qx = a.x + dx * t - px, qy = a.y + dy * t - py;
        return Mathf.Sqrt(qx * qx + qy * qy);
    }

    static void Blend(Color32[] px, int i, Color src, float cover)
    {
        if (cover <= 0f) return;
        float sa = src.a * cover;
        Color32 d = px[i];
        float da = d.a / 255f;
        float oa = sa + da * (1f - sa);
        if (oa <= 0f) return;
        float r = (src.r * sa + (d.r / 255f) * da * (1f - sa)) / oa;
        float g = (src.g * sa + (d.g / 255f) * da * (1f - sa)) / oa;
        float b = (src.b * sa + (d.b / 255f) * da * (1f - sa)) / oa;
        px[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(oa) * 255f));
    }

    // ================================================================ общие помощники
    static Image Img(Transform parent, string name, Sprite sprite, bool sliced, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = layer;
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.sprite = sprite;
        im.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        im.color = Color.white;
        im.raycastTarget = false;
        return im;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                               FontStyles style, TMP_FontAsset font, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = layer;
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static T FindOne<T>() where T : Object
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }

    static void Info(string msg)
    {
        EditorUtility.DisplayDialog("Candy HUD", msg, "OK");
    }
}
#endif
