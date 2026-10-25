#if UNITY_EDITOR
// Assets/_Project/Editor/UI/HudKitBuilder.cs   (в папке Editor)
// Меню: Tools > BuilderSim > Skin Existing HUD (Kit)   (и автоматически в конце Tools > BuilderSim > Build HUD)
//
// Раньше HudKitSkin перекрашивал HUD при каждом запуске игры. Теперь то же самое делается один раз в Editor:
// уровень и опыт, задание, мини-карта и большая карта, подсказка [E] получают дизайн UiKit прямо в сцене.
// Спрайты UiKit рисуются в памяти и в сцене не сохраняются, поэтому нужные сохраняются в PNG (ResourcePanelBuilder.KitSprite).
// Старые слои (Outline, Gloss, Rim, Ring, Fill, Tick) здесь не выключаются, а удаляются: в сцене не остаётся мусора.
// Повторный запуск безопасен: уже обработанное пропускается.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HudKitBuilder
{
    const float PanelBorder = 0.62f;          // уровень (было HudKitSkin.panelBorder)
    const float TaskW = 640f;                 // панель задания не шире, чтобы не налезать на ряд ресурсов слева
    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);

    // ---- спрайты, сохранённые как ассеты (заполняются в Skin)
    static TMP_FontAsset font;
    static Sprite sPanel, sPanelWood, sPlaque, sMedal, sBarFrame, sBarFill, sMapRing, sKeycap, sBtnAmber, sStar;

    [MenuItem("Tools/BuilderSim/UI/Skin Existing HUD (Kit)", false, 902)]
    static void SkinMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Info("Останови Play и запусти ещё раз: в режиме Play изменения сцены не сохраняются.");
            return;
        }
        var hud = GameObject.Find("HUD_Auto");
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (hud == null || ui == null) { Info("Не нашёл HUD_Auto или UIManager. Сначала собери HUD: Tools > BuilderSim > Build HUD."); return; }
        if (!Skin((RectTransform)hud.transform, ui)) return;
        EditorSceneManager.MarkSceneDirty(hud.scene);
        Debug.Log("[HudKit] Готово: HUD перекрашен в сцене. Сохрани сцену (Ctrl+S). HudKitSkin в игре больше не нужен.", hud);
    }

    // Перекрашивает уровень, задание, карты и подсказку внутри hudRoot. Возвращает false, если не удалось сохранить спрайты
    public static bool Skin(RectTransform hudRoot, UIManager ui)
    {
        if (hudRoot == null || ui == null) return false;
        if (!LoadSprites()) return false;
        font = ui.objectiveText != null ? ui.objectiveText.font : null;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Skin HUD (Kit)");
        int undoGroup = Undo.GetCurrentGroup();

        SkinLevel(ui);
        SkinTask(ui);
        foreach (var hm in hudRoot.GetComponentsInChildren<HudMinimap>(true)) SkinMinimap(hm);
        foreach (var ap in hudRoot.GetComponentsInChildren<ActionPrompt>(true)) SkinPrompt(ap);

        Undo.CollapseUndoOperations(undoGroup);
        return true;
    }

    // ================================================================ спрайты
    static bool LoadSprites()
    {
        try
        {
            sPanel = ResourcePanelBuilder.KitSprite("panel", UiKit.Panel);
            sPanelWood = ResourcePanelBuilder.KitSprite("panel_wood", UiKit.PanelWood);
            sPlaque = ResourcePanelBuilder.KitSprite("plaque", UiKit.Plaque);
            sMedal = ResourcePanelBuilder.KitSprite("medallion", UiKit.Medallion);
            sBarFrame = ResourcePanelBuilder.KitSprite("bar_frame", UiKit.BarFrame);
            sBarFill = ResourcePanelBuilder.KitSprite("barfill_" + UiBar.Green, UiKit.BarFill(UiBar.Green));
            sMapRing = ResourcePanelBuilder.KitSprite("map_ring", UiKit.MapRing);
            sKeycap = ResourcePanelBuilder.KitSprite("keycap", UiKit.Keycap);
            sBtnAmber = ResourcePanelBuilder.KitSprite("btn_" + UiBtn.Amber + "_" + UiBtnState.Normal, UiKit.BtnSprite(UiBtn.Amber));
            sStar = ResourcePanelBuilder.KitSprite("icon_" + UiIcon.Star, UiKit.Icon(UiIcon.Star));
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Info("Не удалось сохранить спрайты UiKit (подробности в Console).");
            return false;
        }
        if (sPanel == null || sPanelWood == null || sPlaque == null || sMedal == null || sBarFrame == null ||
            sBarFill == null || sMapRing == null || sKeycap == null || sBtnAmber == null || sStar == null)
        {
            Info("Спрайты UiKit не загрузились как Sprite. Запусти Tools > BuilderSim > UI Kit > Экспорт спрайтов в PNG и повтори.");
            return false;
        }
        return true;
    }

    // ================================================================ уровень и опыт
    static void SkinLevel(UIManager ui)
    {
        if (ui.levelText == null) return;
        var panel = ui.levelText.transform.parent as RectTransform;
        if (panel == null) return;
        StylePanel(panel, sPanel, PanelBorder);

        var badge = panel.Find("Badge") as RectTransform;
        if (badge != null) StyleBadge(badge, sStar);
        FitOneLine(ui.levelText, 22f, 32f);

        var fill = ui.xpFillBar;
        if (fill == null) return;
        var clip = fill.transform.parent;
        var track = clip != null ? clip.parent : null;
        var bar = track != null ? track.parent : null;
        if (bar == null) return;

        const float bs = 0.6f;
        var bi = bar.GetComponent<Image>();
        if (bi != null)
        {
            Undo.RecordObject(bi, "Skin HUD (Kit)");
            bi.sprite = sBarFrame;
            bi.type = Image.Type.Sliced;
            bi.pixelsPerUnitMultiplier = 1f / bs;
            bi.color = Color.white;
        }
        var ti = track.GetComponent<Image>();
        if (ti != null) { Undo.RecordObject(ti, "Skin HUD (Kit)"); ti.color = new Color(1f, 1f, 1f, 0f); }     // подложка теперь внутри рамки полосы
        var tr = (RectTransform)track;
        Undo.RecordObject(tr, "Skin HUD (Kit)");
        tr.offsetMin = new Vector2(5f * bs, 5f * bs);
        tr.offsetMax = new Vector2(-5f * bs, -5f * bs);

        Undo.RecordObject(fill, "Skin HUD (Kit)");
        fill.sprite = sBarFill;
        fill.color = Color.white;
        for (int i = 1; i <= 3; i++) Kill(clip, "Tick" + i);           // деления уже нарисованы в заливке
        EditorUtility.SetDirty(fill);
    }

    // ================================================================ задание
    static void SkinTask(UIManager ui)
    {
        if (ui.objectiveText == null) return;
        var task = ui.objectiveText.transform.parent as RectTransform;
        if (task == null) return;
        Undo.RecordObject(task, "Skin HUD (Kit)");
        if (task.sizeDelta.x > TaskW) task.sizeDelta = new Vector2(TaskW, task.sizeDelta.y);
        StylePanel(task, sPanel, 0.8f);

        // плашка «ЗАДАНИЕ» становится деревянной табличкой
        var chip = task.Find("Chip") as RectTransform;
        if (chip != null)
        {
            StylePanel(chip, sPlaque, 0.5f);
            var lbl = chip.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null)
            {
                Undo.RecordObject(lbl, "Skin HUD (Kit)");
                lbl.color = WoodText;
                lbl.fontStyle = FontStyles.Bold;
            }
        }

        // иконка задания в медальоне
        var qi = task.Find("QuestIcon") as RectTransform;
        if (qi != null && task.Find("KitMedallion") == null)
        {
            var med = NewImage(task, "KitMedallion", sMedal, false);
            var r = med.rectTransform;
            r.anchorMin = qi.anchorMin; r.anchorMax = qi.anchorMax; r.pivot = qi.pivot;
            r.anchoredPosition = qi.anchoredPosition;
            r.sizeDelta = new Vector2(76f, 76f);
            r.SetSiblingIndex(qi.GetSiblingIndex());
            Undo.RecordObject(qi, "Skin HUD (Kit)");
            qi.sizeDelta = new Vector2(44f, 44f);
        }
    }

    // ================================================================ мини-карта и большая карта
    static void SkinMinimap(HudMinimap hm)
    {
        var root = hm.transform as RectTransform;
        if (root == null) return;

        var frame = root.Find("Frame");
        if (frame != null)
        {
            var fi = frame.GetComponent<Image>();
            if (fi != null)
            {
                Undo.RecordObject(fi, "Skin HUD (Kit)");
                fi.sprite = sMapRing; fi.type = Image.Type.Simple; fi.color = Color.white;
            }
        }
        var clip = root.Find("Clip") as RectTransform;
        if (clip != null)
        {
            Undo.RecordObject(clip, "Skin HUD (Kit)");
            clip.offsetMin = new Vector2(14f, 14f);
            clip.offsetMax = new Vector2(-14f, -14f);
        }

        // «N» на рамке: латунная заливка, тёмная буква
        var north = root.Find("North");
        if (north != null)
        {
            var f = north.Find("Fill");
            var fi = f != null ? f.GetComponent<Image>() : null;
            if (fi != null) { Undo.RecordObject(fi, "Skin HUD (Kit)"); fi.color = UiKit.Brass; }
            var nt = north.GetComponentInChildren<TMP_Text>(true);
            if (nt != null) { Undo.RecordObject(nt, "Skin HUD (Kit)"); nt.color = UiKit.Navy; }
        }

        if (hm.zoneBanner != null) StylePanel(hm.zoneBanner, sPanel, 0.5f);
        BuildDayBanner(hm, root);
        BuildMapHint(root);

        // большая карта: деревянный заголовок и янтарная кнопка закрытия
        var win = hm.bigWindow;
        if (win != null)
        {
            StylePanel(win, sPanelWood, 1.1f);
            var title = win.Find("Title") as RectTransform;
            if (title != null)
            {
                Undo.RecordObject(title, "Skin HUD (Kit)");
                title.offsetMin = new Vector2(20f, -42f);
                title.offsetMax = new Vector2(-80f, -2f);
                var tt = title.GetComponent<TMP_Text>();
                if (tt != null) { Undo.RecordObject(tt, "Skin HUD (Kit)"); tt.color = WoodText; }
            }
            var close = win.Find("Close") as RectTransform;
            if (close != null)
            {
                Undo.RecordObject(close, "Skin HUD (Kit)");
                close.sizeDelta = new Vector2(48f, 48f);
                close.anchoredPosition = new Vector2(-12f, -2f);
                var ci = close.GetComponent<Image>();
                if (ci != null)
                {
                    Undo.RecordObject(ci, "Skin HUD (Kit)");
                    ci.sprite = sBtnAmber;
                    ci.type = Image.Type.Sliced;
                    ci.pixelsPerUnitMultiplier = 1f / 0.7f;
                    ci.color = Color.white;
                }
                Kill(close, "Fill");
                var ct = close.GetComponentInChildren<TMP_Text>(true);
                if (ct != null) { Undo.RecordObject(ct, "Skin HUD (Kit)"); ct.color = UiKit.Navy; }
            }
        }
        EditorUtility.SetDirty(hm);
    }

    // ================================================================ табличка «День N» и подсказка «M · Карта»
    // Деревянная табличка прижата под кругом карты и чуть заходит под плашку зоны (она рисуется поверх).
    // Выше/ниже: DayTopY (меньше по модулю = выше)
    const float DayTopY = -10f, DayW = 212f, DayH = 42f;
    const float HintGap = 4f, HintW = 124f, HintH = 36f;

    static void BuildDayBanner(HudMinimap hm, RectTransform root)
    {
        var rt = root.Find("DayBanner") as RectTransform;
        if (rt == null)
        {
            var go = new GameObject("DayBanner", typeof(RectTransform), typeof(Image));
            go.layer = root.gameObject.layer;
            Undo.RegisterCreatedObjectUndo(go, "Skin HUD (Kit)");
            rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, DayTopY);
            rt.sizeDelta = new Vector2(DayW, DayH);
            var im = go.GetComponent<Image>();
            im.sprite = sPlaque;
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = 1f / 0.55f;
            im.raycastTarget = false;

            var tx = NewText(rt, "Text", "День 1", 26f, WoodText, TextAlignmentOptions.Center, hm.zoneLabel);
            tx.rectTransform.offsetMin = new Vector2(12f, 3f);
            tx.rectTransform.offsetMax = new Vector2(-12f, -1f);
            tx.enableAutoSizing = true;
            tx.fontSizeMin = 16f;
            tx.fontSizeMax = 26f;

            // плашка зоны должна лежать поверх таблички
            if (hm.zoneBanner != null && hm.zoneBanner.parent == root) rt.SetSiblingIndex(hm.zoneBanner.GetSiblingIndex());
        }

        var text = rt.Find("Text") != null ? rt.Find("Text").GetComponent<TMP_Text>() : null;
        Undo.RecordObject(hm, "Skin HUD (Kit)");
        hm.dayBanner = rt;
        hm.dayText = text;
        hm.phaseColor = new Color32(0x6B, 0x44, 0x23, 255);      // приписка «· Закат» тёмно-коричневая, читается на дереве
        EditorUtility.SetDirty(hm);
    }

    // [M] Карта: латунная клавиша-кейкап и подпись в тёмной капсуле, по центру под табличкой дня
    static void BuildMapHint(RectTransform root)
    {
        var old = root.Find("MapHint");
        if (old != null && old.Find("Keycap") == null) Undo.DestroyObjectImmediate(old.gameObject);   // старая синяя подсказка
        if (root.Find("MapHint") != null) return;

        var go = new GameObject("MapHint", typeof(RectTransform), typeof(Image));
        go.layer = root.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(go, "Skin HUD (Kit)");
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, DayTopY - DayH - HintGap);
        rt.sizeDelta = new Vector2(HintW, HintH);
        var bg = go.GetComponent<Image>();
        bg.sprite = sPanel;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 1f / 0.45f;
        bg.raycastTarget = false;

        var key = NewImage(rt, "Keycap", sKeycap, false);
        var kr = key.rectTransform;
        kr.anchorMin = kr.anchorMax = new Vector2(0f, 0.5f);
        kr.pivot = new Vector2(0.5f, 0.5f);
        kr.anchoredPosition = new Vector2(24f, 0f);
        kr.sizeDelta = new Vector2(32f, 32f);
        var kt = NewText(kr, "Text", "M", 20f, UiKit.Navy, TextAlignmentOptions.Center, null);
        kt.rectTransform.offsetMin = new Vector2(0f, 1f);
        kt.rectTransform.offsetMax = new Vector2(0f, 1f);

        var lt = NewText(rt, "Label", "Карта", 21f, UiKit.BrassHi, TextAlignmentOptions.MidlineLeft, null);
        lt.rectTransform.offsetMin = new Vector2(46f, 0f);
        lt.rectTransform.offsetMax = new Vector2(-10f, 0f);
        lt.enableAutoSizing = true;
        lt.fontSizeMin = 14f;
        lt.fontSizeMax = 21f;
    }

    static TextMeshProUGUI NewText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align, TMP_Text fontFrom)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(go, "Skin HUD (Kit)");
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        var t = go.GetComponent<TextMeshProUGUI>();
        var f = fontFrom != null ? fontFrom.font : font;
        if (f != null) t.font = f;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    // ================================================================ плашка действия [E] Рубить
    static void SkinPrompt(ActionPrompt ap)
    {
        var root = ap.transform as RectTransform;
        if (root == null) return;
        StylePanel(root, sPanel, 0.7f);

        Undo.RecordObject(ap, "Skin HUD (Kit)");
        if (ap.keyRect != null)
        {
            var ki = ap.keyRect.GetComponent<Image>();
            if (ki != null)
            {
                Undo.RecordObject(ki, "Skin HUD (Kit)");
                ki.sprite = sKeycap;
                ki.type = Image.Type.Simple;
                ki.color = Color.white;
                ap.keyImage = ki;                      // красится при «Рюкзак полон»
                ap.keyColor = Color.white;
            }
            Kill(ap.keyRect, "Fill");
        }
        if (ap.keyText != null)
        {
            Undo.RecordObject(ap.keyText, "Skin HUD (Kit)");
            ap.keyText.color = UiKit.Navy;
            ap.keyText.enableAutoSizing = false;
            ap.keyText.fontSize = 30f;
        }
        EditorUtility.SetDirty(ap);
    }

    // ================================================================ общие помощники
    // Панель из BuilderSimHud (Shadow/Outline/Body/Gloss/Rim/Ring): тело получает спрайт UiKit, лишние слои удаляются
    static void StylePanel(RectTransform rt, Sprite sprite, float borderScale)
    {
        if (rt == null) return;
        Kill(rt, "Outline");
        Kill(rt, "Gloss");
        Kill(rt, "Rim");
        Kill(rt, "Ring");

        var body = rt.Find("Body") as RectTransform;
        if (body == null) return;
        Undo.RecordObject(body, "Skin HUD (Kit)");
        body.offsetMin = body.offsetMax = Vector2.zero;
        var im = body.GetComponent<Image>();
        if (im == null) return;
        Undo.RecordObject(im, "Skin HUD (Kit)");
        im.sprite = sprite;
        im.type = Image.Type.Sliced;
        im.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, borderScale);
        im.color = Color.white;
    }

    // круглый бейдж: прежние слои удаляются, под иконкой появляется латунный медальон UiKit
    static void StyleBadge(RectTransform badge, Sprite icon)
    {
        Kill(badge, "Outline");
        Kill(badge, "Fill");
        Kill(badge, "Gloss");
        Kill(badge, "Ring");

        var ic = badge.Find("Icon") as RectTransform;
        if (badge.Find("KitMedallion") == null)
        {
            var med = NewImage(badge, "KitMedallion", sMedal, false);
            var r = med.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.SetSiblingIndex(ic != null ? ic.GetSiblingIndex() : badge.childCount - 1);
        }
        if (ic != null)
        {
            var im = ic.GetComponent<Image>();
            if (im != null)
            {
                Undo.RecordObject(im, "Skin HUD (Kit)");
                im.sprite = icon; im.color = Color.white; im.preserveAspect = true;
            }
            Undo.RecordObject(ic, "Skin HUD (Kit)");
            float s = Mathf.Max(20f, badge.rect.width) * 0.62f;
            ic.sizeDelta = new Vector2(s, s);
        }
    }

    static void FitOneLine(TMP_Text t, float min, float max)
    {
        Undo.RecordObject(t, "Skin HUD (Kit)");
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.enableWordWrapping = false;
        EditorUtility.SetDirty(t);
    }

    static Image NewImage(Transform parent, string name, Sprite sprite, bool sliced)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(go, "Skin HUD (Kit)");
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.sprite = sprite;
        im.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        im.color = Color.white;
        im.raycastTarget = false;
        return im;
    }

    static void Kill(Transform parent, string child)
    {
        var c = parent != null ? parent.Find(child) : null;
        if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
    }

    static void Info(string msg)
    {
        EditorUtility.DisplayDialog("HUD Kit", msg, "OK");
    }
}
#endif
