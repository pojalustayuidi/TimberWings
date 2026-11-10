using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TWHudPolishBuilder
{
    const string Menu = "Tools/BuilderSim/UI/Игровой HUD/";
    const string Art = "Assets/_Project/Art/UI/HudPolish";
    static HudPolishRoot owner;
    static TMP_FontAsset font;

    [MenuItem(Menu + "1. Обновить игровой HUD", false, 220)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("HUD: останови Play перед сборкой."); return; }
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) { Debug.LogError("HUD: сначала сохрани игровую сцену."); return; }
        int group = -1;
        try
        {
            var ui = One<UIManager>(scene);
            if (ui == null || ui.coinText == null || ui.levelText == null) throw new InvalidOperationException("В активной сцене нужен UIManager с назначенными ресурсами и уровнем.");
            var skin = LoadSkin(); // Validate assets before modifying a scene.
            string backupDir = "UserSettings/TW_HudBackups/" + scene.name;
            Directory.CreateDirectory(backupDir);
            string backup = backupDir + "/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Не удалось сохранить резервную копию сцены.");
            Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("TimberWings: обновить HUD");
            var hud = One<CandyHud>(scene);
            if (hud == null)
            {
                EditorApplication.ExecuteMenuItem("Tools/BuilderSim/UI/Candy Kit/Собрать HUD (ресурсы, задание, уровень)");
                hud = One<CandyHud>(scene);
            }
            if (hud == null) throw new InvalidOperationException("Не удалось найти CandyHud. Сначала собери HUD штатным меню Candy Kit.");
            owner = hud.GetComponent<HudPolishRoot>();
            if (owner == null) owner = Undo.AddComponent<HudPolishRoot>(hud.gameObject);
            Undo.RegisterCompleteObjectUndo(owner, "HUD: ссылки и откат");
            if (owner.created.Count > 0) RemoveCreated(owner);
            owner.skin = skin; owner.sceneBackup = backup;
            owner.canvas = hud.GetComponentInParent<Canvas>();
            if (owner.canvas != null) owner.canvas = owner.canvas.rootCanvas;
            if (owner.canvas == null) throw new InvalidOperationException("CandyHud должен находиться внутри Canvas.");
            owner.resources = hud.transform.Find("CandyResources") as RectTransform;
            owner.level = hud.transform.Find("Level") as RectTransform;
            owner.task = hud.GetComponentInChildren<CandyTaskView>(true);
            var map = One<HudMinimap>(scene); owner.minimap = map != null ? map.transform as RectTransform : null;
            var action = One<ActionPrompt>(scene); owner.action = action != null ? action.transform as RectTransform : null;
            if (owner.resources == null || owner.level == null || owner.task == null) throw new InvalidOperationException("Неполный CandyHud: нужны CandyResources, Level и CandyTask.");
            Undo.RecordObject(owner.task, "HUD: задание");
            font = ui.coinText.font != null ? ui.coinText.font : ui.objectiveText != null ? ui.objectiveText.font : null;
            if (font == null) throw new InvalidOperationException("Не найден шрифт HUD с кириллицей. Назначь шрифт ресурсам в UIManager.");
            Capture(owner.resources); Capture(owner.level); Capture(owner.task.panel);
            if (owner.before.task == null)
            {
                var t = owner.task; owner.before.task = t;
                owner.before.taskWidth = t.width; owner.before.taskSide = t.sidePad; owner.before.taskTop = t.topPad;
                owner.before.taskBottom = t.bottomPad; owner.before.taskRow = t.rowHeight; owner.before.taskGap = t.rowGap;
            }
            if (map != null && owner.before.map == null) { owner.before.map = map; owner.before.stackGap = map.stackGap; }
            // Legacy intro remembers old screen coordinates; do not let it drag the authored panels back.
            foreach (var intro in owner.canvas.GetComponentsInChildren<HudIntro>(true)) Disable(intro);
            var watches = new List<HudPolishRoot.SparkWatch>();
            Resources(watches); Level(ui, watches); Quest();
            if (map != null) Map(map);
            if (action != null) Action(action);
            owner.rewards = watches.ToArray();
            Tooltip();
            Canvas.ForceUpdateCanvases();
            owner.Reflow();
            EditorUtility.SetDirty(owner);
            var problems = Inspect(scene);
            if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud.gameObject;
            Debug.Log("HUD: обновлён. Ресурсы, задание, уровень, миникарта и подсказка сохранены в сцене. Рюкзак и кнопки используют те же спрайты в Play. Ctrl+S. Резервная копия: " + backup);
        }
        catch (Exception ex)
        {
            if (group >= 0) Undo.RevertAllDownToGroup(group);
            Debug.LogError("HUD: сборка отменена, изменения сцены отменены. " + ex);
        }
        finally { owner = null; font = null; }
    }

    static T One<T>(Scene scene) where T : Component
    {
        T result = null;
        foreach (var root in scene.GetRootGameObjects()) foreach (var item in root.GetComponentsInChildren<T>(true))
        {
            if (!item.gameObject.activeInHierarchy) continue;
            if (result != null) throw new InvalidOperationException("В активной сцене несколько " + typeof(T).Name + ". Выбери одну игровую сцену/HUD.");
            result = item;
        }
        return result;
    }
    static Sprite Need(string key)
    {
        string path = CandyUiKitGenerator.OutDir + "/" + key + ".png";
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null || !CandyUiKitGenerator.HasPngSignature(path)) throw new InvalidOperationException("Нет готового спрайта " + path + ". Скопируй весь Assets из архива и настрой импорт Candy Kit.");
        return s;
    }
    static HudPolishRoot.SkinSet LoadSkin()
    {
        var s = new HudPolishRoot.SkinSet {
            chip = Need("Panels/panel_chip"), toast = Need("Panels/panel_toast"), quest = Need("Panels/panel_quest"), sign = Need("Panels/sign_wood"),
            backpack = Need("Icons/icon_backpack"), star = Need("Icons/icon_star"), sparkle = Need("Fun/fx_sparkle_small"),
            coin = Need("Icons/icon_coin"), log = Need("Icons/icon_log"), planks = Need("Icons/icon_planks"),
            paw = Need("Icons/icon_paw"), trophy = Need("Icons/icon_trophy"), check = Need("Icons/icon_check") };
        string[] states = { "normal", "hover", "pressed", "disabled" };
        for (int i = 0; i < 4; i++) { s.gold[i] = Need("Buttons/btn_yellow_" + states[i]); s.mint[i] = Need("Buttons/btn_mint_" + states[i]); s.blue[i] = Need("Buttons/btn_blue_" + states[i]); }
        Directory.CreateDirectory(Art);
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            s.medal = Bake("medallion", UiKit.Medallion); s.keycap = Bake("keycap", UiKit.Keycap);
            s.tail = Bake("tail", UiKit.Tail); s.mapRing = Bake("map_ring", UiKit.MapRing);
            s.pill = Bake("pill", UiDraw.Pill); s.arrow = Bake("arrow_up", UiKit.Icon(UiIcon.ArrowUp));
        }
        s.reticle = Reticle();
        foreach (var sprite in new[] { s.chip, s.quest, s.sign, s.medal, s.keycap, s.tail, s.mapRing, s.reticle, s.backpack })
            if (sprite == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                throw new InvalidOperationException("Спрайт оформления не сохранён как постоянный asset.");
        return s;
    }
    static Sprite Bake(string key, Sprite source)
    {
        string path = Art + "/" + key + ".png";
        if (!File.Exists(path))
        {
            if (source == null || !source.texture.isReadable) throw new InvalidOperationException("Нельзя запечь редакторский спрайт " + key);
            File.WriteAllBytes(path, source.texture.EncodeToPNG());
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) throw new InvalidOperationException("Не импортировался " + path);
        CandyUiKitGenerator.ConfigureImporter(ti, source != null ? source.border : Vector4.zero);
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite Reticle()
    {
        string path = Art + "/reticle.png";
        if (!File.Exists(path))
        {
            const int n = 128; var texture = new Texture2D(n, n, TextureFormat.RGBA32, false); var pixels = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = x + .5f - n / 2f, dy = y + .5f - n / 2f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float shape = Mathf.Min(Mathf.Abs(r - 31f) - 1.3f, r - 3f);
                // Four short compass strokes; they leave the exact aim point visible.
                float tick = Mathf.Min(Mathf.Max(Mathf.Abs(dx) - 1.3f, Mathf.Abs(Mathf.Abs(dy) - 46f) - 4f), Mathf.Max(Mathf.Abs(dy) - 1.3f, Mathf.Abs(Mathf.Abs(dx) - 46f) - 4f));
                shape = Mathf.Min(shape, tick);
                Color c = Color.Lerp(HudPolishRoot.Ink, Color.white, Mathf.Clamp01(.5f - shape));
                c.a = Mathf.Clamp01(2.2f - shape); pixels[y * n + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        }
        return Bake("reticle", null);
    }
    static void Capture(RectTransform r)
    {
        if (r == null) return;
        Undo.RecordObject(r, "HUD: раскладка");
        foreach (var s in owner.before.rects) if (s.target == r) return;
        Undo.RecordObject(r, "HUD: раскладка");
        owner.before.rects.Add(new HudPolishSnapshot.RectState { target = r, min = r.anchorMin, max = r.anchorMax, pivot = r.pivot,
            position = r.anchoredPosition, size = r.sizeDelta, scale = r.localScale, rotation = r.localRotation });
    }
    static void Capture(Image image)
    {
        if (image == null) return; Capture(image.rectTransform);
        Undo.RecordObject(image, "HUD: графика");
        foreach (var s in owner.before.images) if (s.target == image) return;
        Undo.RecordObject(image, "HUD: графика");
        owner.before.images.Add(new HudPolishSnapshot.ImageState { target = image, sprite = image.sprite, color = image.color, type = image.type,
            ppu = image.pixelsPerUnitMultiplier, enabled = image.enabled, aspect = image.preserveAspect, raycast = image.raycastTarget });
    }
    static void Capture(TMP_Text text)
    {
        if (text == null) return; Capture(text.rectTransform);
        Undo.RecordObject(text, "HUD: текст");
        foreach (var s in owner.before.texts) if (s.target == text) return;
        Undo.RecordObject(text, "HUD: текст");
        owner.before.texts.Add(new HudPolishSnapshot.TextState { target = text, font = text.font, material = text.fontSharedMaterial,
            size = text.fontSize, min = text.fontSizeMin, max = text.fontSizeMax, outline = text.outlineWidth, color = text.color,
            style = text.fontStyle, autosize = text.enableAutoSizing, align = text.alignment, wrap = text.textWrappingMode });
    }
    static void Disable(Behaviour b)
    {
        if (b == null) return;
        bool known = false; foreach (var s in owner.before.behaviours) if (s.target == b) { known = true; break; }
        if (!known) owner.before.behaviours.Add(new HudPolishSnapshot.EnabledState { target = b, value = b.enabled });
        Undo.RecordObject(b, "HUD: старый декор"); b.enabled = false;
    }
    static RectTransform New(string name, Transform parent)
    {
        var go = new GameObject("TW_Hud_" + name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(go, "HUD: декор"); go.transform.SetParent(parent, false); owner.created.Add(go);
        return go.transform as RectTransform;
    }
    static void Stretch(RectTransform r, float l = 0, float b = 0, float right = 0, float top = 0)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-right, -top); }
    static Image Img(string name, Transform parent, Sprite sprite, bool sliced = false)
    {
        var r = New(name, parent); var im = Undo.AddComponent<Image>(r.gameObject); im.sprite = sprite;
        im.type = sliced ? Image.Type.Sliced : Image.Type.Simple; im.color = Color.white; im.raycastTarget = false; return im;
    }
    static TMP_Text Label(string name, Transform parent, string value, float size)
    {
        var r = New(name, parent); var t = Undo.AddComponent<TextMeshProUGUI>(r.gameObject); t.font = font; t.text = value;
        t.fontSize = size; t.color = HudPolishRoot.Ink; t.fontStyle = FontStyles.Bold; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; return t;
    }
    static void Text(TMP_Text t, float size, bool bold = true)
    {
        if (t == null) return; Capture(t); t.font = font; t.color = HudPolishRoot.Ink; t.outlineWidth = 0;
        t.fontSize = size; t.fontSizeMax = size; t.fontSizeMin = Mathf.Min(18, size); t.enableAutoSizing = true;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
    }
    static Image Background(RectTransform rect, Sprite sprite, float border = 1f)
    {
        Capture(rect);
        var old = rect.GetComponent<Image>(); if (old != null) { Capture(old); old.enabled = false; }
        string[] legacy = { "Outline", "Body", "Gloss", "Rim", "Ring", "Shadow", "Back" };
        foreach (string key in legacy)
        {
            var child = rect.Find(key); var im = child != null ? child.GetComponent<Image>() : null;
            if (im != null) { Capture(im); im.enabled = false; }
        }
        var bg = Img("Back", rect, sprite, true); Stretch(bg.rectTransform); bg.rectTransform.SetAsFirstSibling(); bg.pixelsPerUnitMultiplier = 1f / border;
        return bg;
    }
    static HudPolishRoot.SparkWatch Watch(TMP_Text label, RectTransform parent)
    {
        var w = new HudPolishRoot.SparkWatch { label = label, sparks = new Image[3] };
        for (int i = 0; i < 3; i++)
        {
            var star = Img("RewardSpark" + i, parent, owner.skin.sparkle); star.enabled = false;
            HudPolishRoot.Anchor(star.rectTransform, new Vector2(0, 1), new Vector2(.5f, .5f), new Vector2(20 + i * 17, -6 - i % 2 * 14), new Vector2(17, 17));
            w.sparks[i] = star;
        }
        return w;
    }
    static void Resources(List<HudPolishRoot.SparkWatch> watches)
    {
        string[] names = { "Coins", "Wood", "Planks" }; Sprite[] icons = { owner.skin.coin, owner.skin.log, owner.skin.planks };
        for (int i = 0; i < names.Length; i++)
        {
            var slot = owner.resources.Find(names[i]) as RectTransform; if (slot == null) throw new InvalidOperationException("Нет слота ресурсов " + names[i]);
            Capture(slot); HudPolishRoot.Anchor(slot, new Vector2(0, 1), new Vector2(.5f, .5f), new Vector2(92 + i * 200, -39), new Vector2(184, 78));
            var frame = slot.Find("Frame").GetComponent<Image>(); Capture(frame); frame.sprite = owner.skin.chip; frame.color = Color.white;
            frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1.05f;
            var icon = slot.Find("Icon").GetComponent<Image>(); Capture(icon); icon.sprite = icons[i]; icon.preserveAspect = true;
            HudPolishRoot.Anchor(icon.rectTransform, new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(20, 0), new Vector2(72, 72));
            var value = slot.Find("Value").GetComponent<TMP_Text>(); Text(value, 34); value.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(value.rectTransform, 66, 8, 12, 8);
            var shine = slot.GetComponent<HudShine>(); if (shine != null) Disable(shine);
            watches.Add(Watch(value, slot));
        }
    }
    static void Level(UIManager ui, List<HudPolishRoot.SparkWatch> watches)
    {
        Background(owner.level, owner.skin.chip);
        // CandyHud.EnsureLevelBack sees this authored Back and will not create a procedural rectangle.
        var existing = owner.level.Find("Back");
        if (existing == null)
        { var bg = owner.level.Find("TW_Hud_Back"); if (bg != null) bg.name = "Back"; }
        var star = owner.level.Find("Star").GetComponent<Image>(); Capture(star); star.sprite = owner.skin.star;
        HudPolishRoot.Anchor(star.rectTransform, new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(42, 0), new Vector2(96, 96));
        var plate = owner.level.Find("Plate") as RectTransform; Capture(plate);
        var pi = plate.GetComponent<Image>(); Capture(pi); pi.enabled = false;
        HudPolishRoot.Anchor(plate, new Vector2(0, 1), new Vector2(0, 1), new Vector2(98, -12), new Vector2(232, 38));
        Text(ui.levelText, 28); ui.levelText.alignment = TextAlignmentOptions.MidlineLeft;
        var bar = owner.level.Find("XpBar").GetComponent<Image>(); Capture(bar);
        bar.sprite = Need("Controls/xp_frame"); bar.color = Color.white;
        HudPolishRoot.Anchor(bar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(98, 20), new Vector2(232, 28));
        Capture(ui.xpFillBar); ui.xpFillBar.sprite = Need("Controls/xp_fill"); ui.xpFillBar.color = Color.white;
        Stretch(ui.xpFillBar.rectTransform, 3, 3, 3, 3);
        watches.Add(Watch(ui.levelText, owner.level));
        var notice = Img("LevelNotice", owner.transform, owner.skin.chip, true);
        owner.levelNotice = Undo.AddComponent<CanvasGroup>(notice.gameObject); owner.levelNotice.alpha = 0; owner.levelNotice.blocksRaycasts = false;
        owner.levelNoticeText = Label("LevelNoticeText", notice.transform, "НОВЫЙ УРОВЕНЬ", 18); Stretch(owner.levelNoticeText.rectTransform, 20, 3, 20, 3);
        owner.levelNoticeText.alignment = TextAlignmentOptions.Center;
    }
    static void Quest()
    {
        var view = owner.task; Undo.RecordObject(view, "HUD: задание"); view.width = 520; view.sidePad = 34; view.topPad = 54; view.bottomPad = 28; view.rowHeight = 56; view.rowGap = 4;
        var image = view.panel.GetComponent<Image>(); Capture(image); image.sprite = owner.skin.quest; image.pixelsPerUnitMultiplier = 1.15f;
        Text(view.title, 28); Text(view.footer, 20, false);
        var sign = view.panel.Find("Sign") as RectTransform; Capture(sign);
        var signImage = sign.GetComponent<Image>(); Capture(signImage); signImage.sprite = owner.skin.sign;
        HudPolishRoot.Anchor(sign, new Vector2(.5f, 1), new Vector2(.5f, 0), new Vector2(0, -16), new Vector2(218, 60));
        var label = sign.GetComponentInChildren<TMP_Text>(true); Text(label, 24); label.color = new Color32(93, 48, 19, 255); Stretch(label.rectTransform, 18, 8, 18, 8);
        var hint = Label("QuestHotkey", view.panel, "J · свернуть", 16); hint.color = CandyTag.Muted;
        HudPolishRoot.Anchor(hint.rectTransform, Vector2.one, Vector2.one, new Vector2(-30, -20), new Vector2(116, 25)); hint.alignment = TextAlignmentOptions.MidlineRight;
    }
    static void Map(HudMinimap map)
    {
        Capture(owner.minimap); Undo.RecordObject(map, "HUD: стопка карты"); map.stackGap = 4;
        var frame = owner.minimap.Find("Frame"); if (frame != null) { var im = frame.GetComponent<Image>(); Capture(im); im.sprite = owner.skin.mapRing; im.color = Color.white; }
        if (map.zoneBanner != null)
        {
            Background(map.zoneBanner, owner.skin.chip, .9f); Capture(map.zoneBanner);
            map.zoneBanner.sizeDelta = new Vector2(264, 48); Text(map.zoneLabel, 23); map.zoneLabel.alignment = TextAlignmentOptions.Center;
        }
        if (map.dayBanner != null) { Background(map.dayBanner, owner.skin.chip, .8f); Capture(map.dayBanner); map.dayBanner.sizeDelta = new Vector2(258, 42); }
        if (map.dayText != null) { Text(map.dayText, 20); Stretch(map.dayText.rectTransform, 14, 3, 14, 3); }
        var hint = owner.minimap.Find(map.mapHintName) as RectTransform;
        if (hint != null) { Background(hint, owner.skin.chip, .75f); hint.sizeDelta = new Vector2(160, 34); Text(hint.GetComponentInChildren<TMP_Text>(true), 18); }
        foreach (var b in owner.minimap.GetComponentsInChildren<Button>(true))
        {
            if (b == map.openButton || b.transform.parent != owner.minimap) continue;
            var image = b.GetComponent<Image>(); if (image != null && image.color.a > .5f) { Capture(image); image.sprite = owner.skin.gold[0]; }
        }
        var leaf = Img("MapLeaf", owner.minimap, Need("Extra/cedar_branch")); leaf.preserveAspect = true;
        HudPolishRoot.Anchor(leaf.rectTransform, new Vector2(0, 0), new Vector2(.5f, .5f), new Vector2(9, 22), new Vector2(48, 44)); leaf.rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
        var star = Img("MapStar", owner.minimap, owner.skin.star);
        HudPolishRoot.Anchor(star.rectTransform, Vector2.one, new Vector2(.5f, .5f), new Vector2(-8, -25), new Vector2(28, 28));
        if (map.dayBanner != null)
        {
            var phase = Img("DayAccent", map.dayBanner, owner.skin.sparkle); owner.phaseIcon = phase;
            HudPolishRoot.Anchor(phase.rectTransform, new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(6, 0), new Vector2(18, 18));
        }
    }
    static void Action(ActionPrompt action)
    {
        Background(owner.action, owner.skin.chip);
        Capture(action.keyRect); HudPolishRoot.Anchor(action.keyRect, new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(55, 0), new Vector2(68, 68));
        var edge = action.keyRect.GetComponent<Image>(); if (edge != null) { Capture(edge); edge.enabled = false; }
        if (action.keyImage != null) { Capture(action.keyImage); action.keyImage.sprite = owner.skin.keycap; action.keyImage.type = Image.Type.Simple; action.keyImage.preserveAspect = true; action.keyImage.color = Color.white; }
        Text(action.keyText, 36); Text(action.labelText, 32); Stretch(action.labelText.rectTransform, 108, 12, 24, 12);
    }
    static void Tooltip()
    {
        var tip = Img("Tooltip", owner.transform, owner.skin.chip, true);
        owner.tooltip = Undo.AddComponent<CanvasGroup>(tip.gameObject); owner.tooltip.alpha = 0; owner.tooltip.blocksRaycasts = false;
        owner.tooltipTitle = Label("TooltipTitle", tip.transform, "", 21); owner.tooltipBody = Label("TooltipBody", tip.transform, "", 17);
        Stretch(owner.tooltipTitle.rectTransform, 26, 51, 22, 12); Stretch(owner.tooltipBody.rectTransform, 26, 12, 22, 45);
        owner.tooltipTitle.alignment = owner.tooltipBody.alignment = TextAlignmentOptions.MidlineLeft;
        owner.tooltipBody.textWrappingMode = TextWrappingModes.Normal;
        owner.tooltipBody.overflowMode = TextOverflowModes.Ellipsis;
        owner.tooltipBody.color = CandyTag.Muted; owner.tooltipBody.fontStyle = FontStyles.Normal;
        owner.tooltipBody.enableAutoSizing = true; owner.tooltipBody.fontSizeMin = 14; owner.tooltipBody.fontSizeMax = 17;
    }
    static void RemoveCreated(HudPolishRoot root)
    {
        for (int i = root.created.Count - 1; i >= 0; i--) if (root.created[i] != null) Undo.DestroyObjectImmediate(root.created[i]);
        root.created.Clear();
    }
    static List<string> Inspect(Scene scene)
    {
        var errors = new List<string>(); var root = One<HudPolishRoot>(scene);
        if (root == null) { errors.Add("Не найден HudPolishRoot. Запусти пункт 1."); return errors; }
        if (root.resources == null || root.level == null || root.task == null || root.canvas == null) errors.Add("Не назначены основные части HUD.");
        if (root.skin == null || root.skin.chip == null || root.skin.reticle == null || root.skin.mapRing == null || root.skin.backpack == null) errors.Add("Неполный набор спрайтов.");
        if (root.rewards == null || root.rewards.Length != 4) errors.Add("Нужны четыре набора отклика на ресурсы/уровень.");
        foreach (var go in root.created) if (go != null) foreach (var graphic in go.GetComponents<Graphic>()) if (graphic.raycastTarget) errors.Add("Декор перехватывает клики: " + go.name);
        return errors;
    }
    [MenuItem(Menu + "2. Проверить HUD", false, 221)]
    static void QA()
    {
        try
        {
            var issues = Inspect(SceneManager.GetActiveScene());
            if (issues.Count == 0) Debug.Log("HUD QA OK: ссылки, графика, четыре набора отклика и отсутствие кликов на декоре. Проверь разрешения и Play Mode.");
            else Debug.LogError("HUD QA: " + string.Join("\n", issues));
        }
        catch (Exception ex) { Debug.LogError("HUD QA: " + ex.Message); }
    }
    [MenuItem(Menu + "3. Вернуть прежнюю раскладку", false, 222)]
    static void Revert()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene(); var root = One<HudPolishRoot>(scene); if (root == null) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("HUD: вернуть раскладку");
        foreach (var s in root.before.rects) if (s.target != null) { Undo.RecordObject(s.target, "HUD: раскладка"); s.Restore(); }
        foreach (var s in root.before.images) if (s.target != null) { Undo.RecordObject(s.target, "HUD: графика"); s.Restore(); }
        foreach (var s in root.before.texts) if (s.target != null) { Undo.RecordObject(s.target, "HUD: текст"); s.Restore(); }
        foreach (var s in root.before.behaviours) if (s.target != null) { Undo.RecordObject(s.target, "HUD: компонент"); s.target.enabled = s.value; }
        var before = root.before;
        if (before.task != null)
        {
            Undo.RecordObject(before.task, "HUD: задание"); before.task.width = before.taskWidth; before.task.sidePad = before.taskSide; before.task.topPad = before.taskTop;
            before.task.bottomPad = before.taskBottom; before.task.rowHeight = before.taskRow; before.task.rowGap = before.taskGap;
        }
        if (before.map != null) { Undo.RecordObject(before.map, "HUD: карта"); before.map.stackGap = before.stackGap; }
        RemoveCreated(root); Undo.DestroyObjectImmediate(root); Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("HUD: прежняя раскладка сцены возвращена. Для полного отката скриптов используй Source_Before из архива. Ctrl+S.");
    }
}
