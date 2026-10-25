#if UNITY_EDITOR
// Assets/_Project/Editor/UI/ResourcePanelBuilder.cs   (в папке Editor)
// Меню: Tools > BuilderSim > Build Resource Panel
//
// Собирает В СЦЕНЕ одну общую панель ресурсов «ResourcePanel» (560x76) с тремя слотами Coins / Wood / Planks:
//   Frame (UiKit.Panel) + тонкие разделители + в каждом слоте медальон с иконкой слева (выступает за край слота) и число справа.
// Панель лежит в сцене как обычные объекты и в игре не строится. Повторный запуск безопасен:
// прежний ResourcePanel заменяется новым (позиция сохраняется), старые три панели (Coins/Wood из BuilderSimHud) предлагается убрать.
//
// Что подключается:
//   UIManager.coinText / woodText      -> числа слотов Coins / Wood (шрифт берётся из UIManager.objectiveText)
//   HudGainAnchor (Coin / Wood)        -> на слотах; flyTarget = иконка слота; gainBehind = Frame («+N» выезжает из-под панели)
//   HudPlanks                          -> на слоте Planks (плавное появление, число, точка вылета для ObjectiveView)
//   HudPop на числах, HudShine на слотах (иконка «дышит», по панели бежит блик)
//   HudIntro                           -> ResourcePanel въезжает при старте вместо старых панелей
//   HudSafeAreaElement                 -> на корне; слоты называются Coins / Wood / Planks (по именам их ищет HudSafeAreaRegistry)
//
// Спрайты UiKit рисуются в памяти и в сцене не сохраняются, поэтому нужные (panel, medallion, icon_Coin/Log/Plank)
// один раз сохраняются в PNG в ту же папку, что и Tools > BuilderSim > UI Kit > Экспорт спрайтов в PNG.
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static EditorAssets;

public static class ResourcePanelBuilder
{
    // ---------------------------------------------------------------- настройки (единицы канваса 1920x1080)
    const string RootName = "ResourcePanel";
    const string KitDir = "Assets/_Project/UI/Kit";       // как у UiKitTools
    const string HudArtDir = "Assets/_Game/Art/UI";       // как у BuilderSimHud (там лежит ui_shine.png)

    const float PanelW = 560f, PanelH = 76f;
    const int SlotCount = 3;
    const float SlotW = PanelW / SlotCount;                // 186.67
    const float BorderScale = 0.62f;                       // толщина ободка, как у остального HUD (HudKitBuilder.PanelBorder)

    const float BadgeSize = 64f;                           // медальон
    const float BadgeProtrude = 10f;                       // насколько медальон выступает влево за край слота
    const float IconScale = 0.62f;                         // иконка внутри медальона
    const float ValueLeft = BadgeSize - BadgeProtrude + 8f;
    const float ValueRight = 30f;                          // справа от числа: разделитель и медальон следующего слота
    const float ValueRightLast = 18f;
    const float DividerGap = 20f;                          // разделитель стоит левее границы слотов на столько (медальон следующего слота выступает на 10)
    const float DividerH = PanelH - 28f;

    const float NumberMin = 22f, NumberMax = 34f;          // авторазмер числа; на 1080p не меньше 22 (требование: минимум 18)
    const float OutlineWidth = 0.18f;

    static readonly Vector2 DefaultPos = new Vector2(46f, -40f);   // слева сверху; медальон выступает на 10, поэтому поле 46
    static readonly Vector2 IntroOffset = new Vector2(0f, 160f);   // откуда въезжает при старте (как у старых панелей)
    const float IntroDelay = 0.05f;

    static readonly Color CoinColor = new Color32(0xFF, 0xD6, 0x6B, 255);    // UiKit.BrassHi
    static readonly Color WoodColor = Color.white;
    static readonly Color PlankColor = new Color32(0xE8, 0xC1, 0x7A, 255);

    // ================================================================ меню
    [MenuItem("Tools/BuilderSim/UI/Build Resource Panel", false, 901)]
    static void Build() { Run(false); }

    // Вызывается из Tools > BuilderSim > Build HUD: без вопросов (старые панели удаляются), выделение не меняет.
    // Возвращает корень ResourcePanel или null, если собрать не удалось
    public static RectTransform BuildQuiet() { return Run(true); }

    static RectTransform Run(bool quiet)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Info("Останови Play и запусти ещё раз: в режиме Play изменения сцены не сохраняются.");
            return null;
        }

        var ui = FindOne<UIManager>();
        if (ui == null) { Info("В сцене нет UIManager. Открой сцену с HUD и запусти ещё раз."); return null; }

        // ---- 1. что уже есть в сцене
        var existing = FindExistingRoot(ui);
        var oldPanels = FindOldPanels(ui, existing);

        Transform parent;
        int sibling;
        Vector2 pos = DefaultPos;
        if (existing != null)
        {
            parent = existing.parent; sibling = existing.GetSiblingIndex(); pos = existing.anchoredPosition;
        }
        else if (oldPanels.Count > 0)
        {
            parent = oldPanels[0].parent; sibling = oldPanels[0].GetSiblingIndex();
        }
        else
        {
            Canvas canvas = ui.coinText != null ? ui.coinText.GetComponentInParent<Canvas>(true) : null;
            var hud = canvas != null ? canvas.rootCanvas.transform : (GameObject.Find("HUD_Auto") != null ? GameObject.Find("HUD_Auto").transform : null);
            if (hud == null) { Info("Не нашёл HUD-канвас (HUD_Auto). Сначала собери HUD: Tools > BuilderSim > Build HUD."); return null; }
            parent = hud; sibling = hud.childCount;
        }

        // ---- 2. ресурсы: шрифт, спрайты, материал (до любых изменений сцены, чтобы при ошибке ничего не сломать)
        TMP_FontAsset font = ui.objectiveText != null ? ui.objectiveText.font : null;
        if (font == null && ui.coinText != null) font = ui.coinText.font;
        if (font == null) Debug.LogWarning("[ResourcePanel] Не нашёл шрифт в UIManager.objectiveText: числа получат шрифт TMP по умолчанию (кириллицы может не быть).");

        Sprite sPanel, sMedal, sCoin, sLog, sPlank;
        try
        {
            sPanel = KitSprite("panel", UiKit.Panel);
            sMedal = KitSprite("medallion", UiKit.Medallion);
            sCoin = KitSprite("icon_Coin", UiKit.Icon(UiIcon.Coin));
            sLog = KitSprite("icon_Log", UiKit.Icon(UiIcon.Log));
            sPlank = KitSprite("icon_Plank", UiKit.Icon(UiIcon.Plank));
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Info("Не удалось сохранить спрайты UiKit в " + KitDir + " (подробности в Console).");
            return null;
        }
        if (sPanel == null || sMedal == null || sCoin == null || sLog == null || sPlank == null)
        {
            Info("Спрайты UiKit в " + KitDir + " не загрузились как Sprite. Запусти Tools > BuilderSim > UI Kit > Экспорт спрайтов в PNG и повтори.");
            return null;
        }

        Sprite sShine = AssetDatabase.LoadAssetAtPath<Sprite>(HudArtDir + "/ui_shine.png");   // от BuilderSimHud; нет = панель без бегущего блика
        if (sShine == null) Debug.Log("[ResourcePanel] " + HudArtDir + "/ui_shine.png не найден: блик по панели не рисуется (иконки всё равно «дышат»).");

        Material outline = OutlineMaterial(font);

        // ---- 3. что делать со старыми панелями
        int oldAction = 0;     // 0 = удалить, 2 = отключить
        if (oldPanels.Count > 0 && !quiet)
        {
            var names = new List<string>();
            foreach (var t in oldPanels) names.Add("«" + t.name + "»");
            int choice = EditorUtility.DisplayDialogComplex("Build Resource Panel",
                "Найдены старые панели ресурсов: " + string.Join(", ", names) + ".\n\n" +
                "Пока они активны, в HUD два HudGainAnchor на монеты и дерево: плашки «+N» и полёт брёвен могут уйти в старую панель.\n\n" +
                "Что с ними сделать?",
                "Удалить", "Отмена", "Отключить (не удалять)");
            if (choice == 1) return null;
            oldAction = choice;
        }

        // ---- 4. сборка
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Resource Panel");
        int undoGroup = Undo.GetCurrentGroup();

        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        int layer = parent.gameObject.layer;
        var rootGo = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
        rootGo.layer = layer;
        Undo.RegisterCreatedObjectUndo(rootGo, "Build Resource Panel");
        var root = (RectTransform)rootGo.transform;
        root.SetParent(parent, false);
        root.SetSiblingIndex(Mathf.Min(sibling, parent.childCount - 1));
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = pos;
        root.sizeDelta = new Vector2(PanelW, PanelH);
        var rootCg = rootGo.GetComponent<CanvasGroup>();
        rootCg.blocksRaycasts = false;       // HUD не должен перехватывать клики
        rootCg.interactable = false;
        rootGo.AddComponent<HudSafeAreaElement>();

        // общий фон
        var frame = Img(root, "Frame", sPanel, true, layer);
        frame.pixelsPerUnitMultiplier = 1f / BorderScale;
        Stretch(frame.rectTransform);

        // блик: полоска бежит по маске в форме панели
        RectTransform shineRt = null;
        if (sShine != null)
        {
            var clip = Img(root, "ShineClip", sPanel, true, layer);
            clip.pixelsPerUnitMultiplier = 1f / BorderScale;
            Stretch(clip.rectTransform);
            var mask = clip.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var sh = Img(clip.rectTransform, "Shine", sShine, false, layer);
            sh.color = new Color(1f, 1f, 1f, 0.3f);
            shineRt = sh.rectTransform;
            shineRt.anchorMin = new Vector2(0f, 0f);
            shineRt.anchorMax = new Vector2(0f, 1f);
            shineRt.pivot = new Vector2(0.5f, 0.5f);
            shineRt.sizeDelta = new Vector2(70f, 0f);
            shineRt.anchoredPosition = new Vector2(-200f, 0f);
            sh.gameObject.SetActive(false);      // HudShine включает полоску сам
        }

        // разделители (между слотами)
        var div1 = Divider(root, "Divider1", SlotW * 1f - DividerGap, layer);
        var div2 = Divider(root, "Divider2", SlotW * 2f - DividerGap, layer);

        // слоты
        TMP_Text coinValue, woodValue, plankValue;
        RectTransform coinIcon, woodIcon, plankIcon;
        var coinSlot = Slot(root, "Coins", 0, sMedal, sCoin, CoinColor, ValueRight, font, outline, layer, out coinValue, out coinIcon);
        var woodSlot = Slot(root, "Wood", 1, sMedal, sLog, WoodColor, ValueRight, font, outline, layer, out woodValue, out woodIcon);
        var plankSlot = Slot(root, "Planks", 2, sMedal, sPlank, PlankColor, ValueRightLast, font, outline, layer, out plankValue, out plankIcon);

        // ссылки для «+N», полёта брёвен и иконки цели
        Anchor(coinSlot, HudGain.Kind.Coin, coinIcon, coinValue, frame.rectTransform);
        Anchor(woodSlot, HudGain.Kind.Wood, woodIcon, woodValue, frame.rectTransform);

        // блеск и «дыхание» иконок
        Shine(coinSlot, coinIcon, 0f, shineRt);
        Shine(woodSlot, woodIcon, 0.8f, null);
        Shine(plankSlot, plankIcon, 1.6f, null);

        // слот досок: плавное появление
        var planks = plankSlot.gameObject.AddComponent<HudPlanks>();
        planks.group = plankSlot.gameObject.AddComponent<CanvasGroup>();
        planks.value = plankValue;
        planks.icon = plankIcon;
        planks.panel = root;
        planks.divider = div2;
        planks.baseWidth = SlotW * 2f;
        planks.slotWidth = SlotW;

        // ---- 5. UIManager
        Undo.RecordObject(ui, "Build Resource Panel");
        ui.coinText = coinValue;
        ui.woodText = woodValue;
        EditorUtility.SetDirty(ui);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui);

        // ---- 6. въезд HUD при старте
        var intro = parent.GetComponentInParent<HudIntro>(true);
        if (intro != null)
        {
            Undo.RecordObject(intro, "Build Resource Panel");
            if (intro.items == null) intro.items = new List<HudIntro.Item>();
            intro.items.RemoveAll(it => it == null || it.rt == null || oldPanels.Contains(it.rt));
            intro.items.Add(new HudIntro.Item { rt = root, offset = IntroOffset, delay = IntroDelay });
            EditorUtility.SetDirty(intro);
            PrefabUtility.RecordPrefabInstancePropertyModifications(intro);
        }
        else Debug.LogWarning("[ResourcePanel] HudIntro не найден над панелью: она не будет «въезжать» при старте. Добавь её в HudIntro.items вручную.");

        // ---- 7. старые панели
        foreach (var t in oldPanels)
        {
            if (t == null) continue;
            if (oldAction == 0) Undo.DestroyObjectImmediate(t.gameObject);
            else
            {
                Undo.RecordObject(t.gameObject, "Build Resource Panel");
                t.gameObject.SetActive(false);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        if (!quiet) Selection.activeGameObject = rootGo;
        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        Debug.Log("[ResourcePanel] Готово: ResourcePanel собран (" + PanelW + "x" + PanelH + "). Сохрани сцену (Ctrl+S). " +
                  (oldPanels.Count > 0 ? "Старые панели " + (oldAction == 0 ? "удалены." : "отключены.") : ""), rootGo);
        return root;
    }

    // ================================================================ блоки панели
    // Слот: на всю высоту панели, шириной SlotW. Medallion слева (Badge/Icon), число справа (Value)
    static RectTransform Slot(RectTransform root, string name, int index, Sprite medal, Sprite icon, Color valueColor, float right,
                              TMP_FontAsset font, Material outline, int layer, out TMP_Text value, out RectTransform iconRt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        var slot = (RectTransform)go.transform;
        slot.SetParent(root, false);
        slot.anchorMin = new Vector2(0f, 0f);
        slot.anchorMax = new Vector2(0f, 1f);
        slot.pivot = new Vector2(0.5f, 0.5f);                    // «подпрыгивание» панели (HudGainAnchor.Punch) идёт от центра слота
        slot.sizeDelta = new Vector2(SlotW, 0f);
        slot.anchoredPosition = new Vector2(SlotW * (index + 0.5f), 0f);

        // медальон: левый край на BadgeProtrude левее края слота
        var badge = new GameObject("Badge", typeof(RectTransform));
        badge.layer = layer;
        var br = (RectTransform)badge.transform;
        br.SetParent(slot, false);
        br.anchorMin = br.anchorMax = new Vector2(0f, 0.5f);     // якорь на левом краю слота
        br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(BadgeSize, BadgeSize);
        br.anchoredPosition = new Vector2(-BadgeProtrude + BadgeSize * 0.5f, 0f);

        var ring = Img(br, "KitMedallion", medal, false, layer);
        Stretch(ring.rectTransform);

        var ic = Img(br, "Icon", icon, false, layer);
        ic.preserveAspect = true;
        var icr = ic.rectTransform;
        icr.anchorMin = icr.anchorMax = new Vector2(0.5f, 0.5f);
        icr.pivot = new Vector2(0.5f, 0.5f);
        icr.sizeDelta = Vector2.one * (BadgeSize * IconScale);
        icr.anchoredPosition = Vector2.zero;
        iconRt = icr;

        // число
        var vgo = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        vgo.layer = layer;
        var vr = (RectTransform)vgo.transform;
        vr.SetParent(slot, false);
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.pivot = new Vector2(0f, 0.5f);                        // HudPop раздувает число от левого края
        vr.offsetMin = new Vector2(ValueLeft, 0f);
        vr.offsetMax = new Vector2(-right, 0f);

        var t = vgo.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;                         // сначала шрифт, потом материал
        if (outline != null) t.fontSharedMaterial = outline;
        t.text = "0";
        t.fontSize = NumberMax;
        t.enableAutoSizing = true;
        t.fontSizeMin = NumberMin;
        t.fontSizeMax = NumberMax;
        t.enableWordWrapping = false;                            // «10» не должно переноситься в две строки
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.color = valueColor;
        t.raycastTarget = false;
        vgo.AddComponent<HudPop>();
        value = t;
        return slot;
    }

    static void Anchor(RectTransform slot, HudGain.Kind kind, RectTransform icon, TMP_Text value, RectTransform frame)
    {
        var a = slot.gameObject.AddComponent<HudGainAnchor>();
        a.kind = kind;
        a.flyTarget = icon;          // бревна и иконка цели летят в иконку слота
        a.font = value.font;         // шрифт плашки «+N»
        a.gainBehind = frame;        // плашка выезжает из-под фона панели
    }

    static void Shine(RectTransform slot, RectTransform icon, float delay, RectTransform shine)
    {
        var hs = slot.gameObject.AddComponent<HudShine>();
        hs.breathe = icon;
        hs.delay = delay;
        hs.period = 5.5f;
        hs.shine = shine;
    }

    static Image Divider(RectTransform root, string name, float x, int layer)
    {
        var d = Img(root, name, null, false, layer);
        d.color = new Color(UiKit.BrassLo.r, UiKit.BrassLo.g, UiKit.BrassLo.b, 0.55f);
        var r = d.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(2f, DividerH);
        r.anchoredPosition = new Vector2(x, 0f);
        return d;
    }

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

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // ================================================================ поиск в сцене
    static RectTransform FindExistingRoot(UIManager ui)
    {
        // по цепочке: текст -> слот -> корень
        var p = ui.coinText != null ? ui.coinText.transform.parent : null;
        if (p != null && p.parent != null && p.parent.name == RootName) return p.parent as RectTransform;
        // по имени среди всех объектов сцены (в том числе выключенных)
        foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rt != null && rt.name == RootName && rt.gameObject.scene.IsValid()) return rt;
        return null;
    }

    // старые панели = родители текстов монет и дерева (BuilderSimHud: Coins, Wood), если это не новая панель и не чужая
    static List<Transform> FindOldPanels(UIManager ui, RectTransform newRoot)
    {
        var list = new List<Transform>();
        AddOld(list, ui.coinText, ui, newRoot);
        AddOld(list, ui.woodText, ui, newRoot);
        return list;
    }

    static void AddOld(List<Transform> list, TMP_Text text, UIManager ui, RectTransform newRoot)
    {
        if (text == null) return;
        var p = text.transform.parent;
        if (p == null || list.Contains(p)) return;
        if (newRoot != null && (p == newRoot || p.IsChildOf(newRoot))) return;     // это уже новая панель
        if (p.GetComponent<Canvas>() != null) return;                             // текст лежит прямо на канвасе: панели нет
        if (Holds(p, ui.levelText) || Holds(p, ui.objectiveText) || Holds(p, ui.xpFillBar)) return;   // в панели лежит что-то ещё важное
        list.Add(p);
    }

    static bool Holds(Transform panel, Component c)
    {
        return c != null && (c.transform == panel || c.transform.IsChildOf(panel));
    }

    static T FindOne<T>() where T : Object
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }

    // ================================================================ ассеты
    // UiKit рисует спрайты в памяти; сцена может хранить только ссылки на ассеты. Сохраняем нужный спрайт в PNG (один раз)
    // в ту же папку и с теми же настройками, что и Tools > BuilderSim > UI Kit > Экспорт спрайтов в PNG
    public static Sprite KitSprite(string key, Sprite generated)
    {
        string path = KitDir + "/" + key + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        EnsureFolder(KitDir);
        if (!File.Exists(path)) File.WriteAllBytes(path, generated.texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = generated.pixelsPerUnit;
        imp.spriteBorder = generated.border;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;              // для 9-slice
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // материал шрифта с тёмной обводкой: общий ассет (не копия на каждый текст), чтобы он не слетал при сохранении сцены.
    // Обновляется на месте, ссылки в сценах не рвутся
    static Material OutlineMaterial(TMP_FontAsset font)
    {
        if (font == null || font.material == null) return null;
        EnsureFolder(HudArtDir);
        string path = HudArtDir + "/HUD_ResourceText.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(font.material) { name = "HUD_ResourceText" };
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = font.material.shader;
            m.CopyPropertiesFromMaterial(font.material);         // шрифт мог смениться: берём его атлас
        }
        m.EnableKeyword("OUTLINE_ON");
        if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", OutlineWidth);
        if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", UiKit.Ink);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static void Info(string msg)
    {
        EditorUtility.DisplayDialog("Build Resource Panel", msg, "OK");
    }
}
#endif
