// Assets/_Project/Scripts/UI/HudKitSkin.cs   (НЕ в Editor)
// Перекрашивает уже собранный HUD (Tools > BuilderSim > Build HUD) в стиль UiKit: тёмная панель с латунным ободком и заклёпками,
// круглые медальоны с иконками, сегментная полоса опыта, деревянные таблички, клавиша-«кейкап» в подсказке.
// Скрипт запускается сам при старте (RuntimeInitializeOnLoadMethod), находит панели по UIManager, HudMinimap и ActionPrompt
// и меняет у них только картинки и цвета. Все ссылки и анимации (HudShine, HudPop, HudGain) остаются как были.
// Панель ресурсов (монеты, дерево, доски) здесь больше НЕ строится: её собирает Editor-меню
// Tools > BuilderSim > Build Resource Panel, она хранится в сцене (ResourcePanel).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudKitSkin : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<HudKitSkin>() != null) return;
        new GameObject("HudKitSkin").AddComponent<HudKitSkin>();
    }

    static readonly Color WoodText = new Color32(0x2A, 0x16, 0x08, 255);

    [Tooltip("Масштаб рамки панелей: меньше = тоньше ободок и заклёпки")]
    public float panelBorder = 0.62f;

    readonly HashSet<int> done = new HashSet<int>();
    float nextAt = 0.3f;     // даём HudIntro сохранить финальные позиции панелей

    const float TaskW = 640f;     // панель задания не шире, чтобы не налезать на ряд ресурсов слева

    void Update()
    {
        if (Time.unscaledTime < nextAt) return;
        nextAt = Time.unscaledTime + 0.5f;     // минимапа и подсказка действия могут появиться позже остальных
        Apply();
    }

    void Apply()
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        SkinLevel(ui);
        SkinTask(ui);

        foreach (var hm in FindObjectsByType<HudMinimap>(FindObjectsSortMode.None)) SkinMinimap(hm);
        foreach (var ap in FindObjectsByType<ActionPrompt>(FindObjectsSortMode.None)) SkinPrompt(ap);
    }

    bool Mark(Transform t)
    {
        return t != null && done.Add(t.GetInstanceID());
    }

    // ------------------------------------------------------------ уровень и опыт
    void SkinLevel(UIManager ui)
    {
        if (ui.levelText == null) return;
        var panel = ui.levelText.transform.parent as RectTransform;
        if (panel == null || !Mark(panel)) return;
        StylePanel(panel, UiKit.Panel, panelBorder);

        var badge = panel.Find("Badge") as RectTransform;
        if (badge != null) StyleBadge(badge, UiIcon.Star);
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
            bi.sprite = UiKit.BarFrame;
            bi.type = Image.Type.Sliced;
            bi.pixelsPerUnitMultiplier = 1f / bs;
            bi.color = Color.white;
        }
        var ti = track.GetComponent<Image>();
        if (ti != null) ti.color = new Color(1f, 1f, 1f, 0f);       // подложка теперь внутри рамки полосы
        var tr = (RectTransform)track;
        tr.offsetMin = new Vector2(5f * bs, 5f * bs);
        tr.offsetMax = new Vector2(-5f * bs, -5f * bs);

        fill.sprite = UiKit.BarFill(UiBar.Green);
        fill.color = Color.white;
        for (int i = 1; i <= 3; i++) Off(clip, "Tick" + i);          // деления уже нарисованы в заливке
    }

    // ------------------------------------------------------------ задание
    void SkinTask(UIManager ui)
    {
        if (ui.objectiveText == null) return;
        var task = ui.objectiveText.transform.parent as RectTransform;
        if (task == null || !Mark(task)) return;
        if (task.sizeDelta.x > TaskW) task.sizeDelta = new Vector2(TaskW, task.sizeDelta.y);
        StylePanel(task, UiKit.Panel, 0.8f);

        // плашка «ЗАДАНИЕ» становится деревянной табличкой
        var chip = task.Find("Chip") as RectTransform;
        if (chip != null)
        {
            StylePanel(chip, UiKit.Plaque, 0.5f);
            var lbl = chip.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) { lbl.color = WoodText; lbl.fontStyle = FontStyles.Bold; }
        }

        // иконка задания в медальоне
        var qi = task.Find("QuestIcon") as RectTransform;
        if (qi != null && task.Find("KitMedallion") == null)
        {
            var med = UiKit.MakeImage(task, "KitMedallion", UiKit.Medallion, false);
            var r = med.rectTransform;
            r.anchorMin = qi.anchorMin; r.anchorMax = qi.anchorMax; r.pivot = qi.pivot;
            r.anchoredPosition = qi.anchoredPosition;
            r.sizeDelta = new Vector2(76f, 76f);
            r.SetSiblingIndex(qi.GetSiblingIndex());
            qi.sizeDelta = new Vector2(44f, 44f);
        }
    }

    // ------------------------------------------------------------ мини-карта и большая карта
    void SkinMinimap(HudMinimap hm)
    {
        var root = hm.transform as RectTransform;
        if (root == null || !Mark(root)) return;

        var frame = root.Find("Frame");
        if (frame != null)
        {
            var fi = frame.GetComponent<Image>();
            if (fi != null) { fi.sprite = UiKit.MapRing; fi.type = Image.Type.Simple; fi.color = Color.white; }
        }
        var clip = root.Find("Clip") as RectTransform;
        if (clip != null) { clip.offsetMin = new Vector2(14f, 14f); clip.offsetMax = new Vector2(-14f, -14f); }

        // «N» на рамке: латунная заливка, тёмная буква
        var north = root.Find("North");
        if (north != null)
        {
            var f = north.Find("Fill");
            var fi = f != null ? f.GetComponent<Image>() : null;
            if (fi != null) fi.color = UiKit.Brass;
            var nt = north.GetComponentInChildren<TMP_Text>(true);
            if (nt != null) nt.color = UiKit.Navy;
        }

        if (hm.zoneBanner != null) StylePanel(hm.zoneBanner, UiKit.Panel, 0.5f);
        var hint = root.Find("MapHint") as RectTransform;
        if (hint != null)
        {
            StylePanel(hint, UiKit.Panel, 0.45f);
            hint.pivot = new Vector2(1f, 1f);
            hint.anchoredPosition = new Vector2(-10f, -22f);
        }

        // большая карта: деревянный заголовок и янтарная кнопка закрытия
        var win = hm.bigWindow;
        if (win != null)
        {
            StylePanel(win, UiKit.PanelWood, 1.1f);
            var title = win.Find("Title") as RectTransform;
            if (title != null)
            {
                title.offsetMin = new Vector2(20f, -42f);
                title.offsetMax = new Vector2(-80f, -2f);
                var tt = title.GetComponent<TMP_Text>();
                if (tt != null) tt.color = WoodText;
            }
            var close = win.Find("Close") as RectTransform;
            if (close != null)
            {
                close.sizeDelta = new Vector2(48f, 48f);
                close.anchoredPosition = new Vector2(-12f, -2f);
                var ci = close.GetComponent<Image>();
                if (ci != null)
                {
                    ci.sprite = UiKit.BtnSprite(UiBtn.Amber);
                    ci.type = Image.Type.Sliced;
                    ci.pixelsPerUnitMultiplier = 1f / 0.7f;
                    ci.color = Color.white;
                }
                Off(close, "Fill");
                var ct = close.GetComponentInChildren<TMP_Text>(true);
                if (ct != null) ct.color = UiKit.Navy;
            }
        }
    }

    // ------------------------------------------------------------ плашка действия [E] Рубить
    void SkinPrompt(ActionPrompt ap)
    {
        var root = ap.transform as RectTransform;
        if (root == null || !Mark(root)) return;
        StylePanel(root, UiKit.Panel, 0.7f);

        if (ap.keyRect != null)
        {
            var ki = ap.keyRect.GetComponent<Image>();
            if (ki != null)
            {
                ki.sprite = UiKit.Keycap;
                ki.type = Image.Type.Simple;
                ki.color = Color.white;
                ap.keyImage = ki;                      // красится при «Рюкзак полон»
                ap.keyColor = Color.white;
            }
            Off(ap.keyRect, "Fill");
        }
        if (ap.keyText != null)
        {
            ap.keyText.color = UiKit.Navy;
            ap.keyText.enableAutoSizing = false;
            ap.keyText.fontSize = 30f;
        }
    }

    // ------------------------------------------------------------ общие помощники
    // Панель из BuilderSimHud (Shadow/Outline/Body/Gloss/Rim/Ring): тело получает спрайт UiKit, остальные слои выключаются
    void StylePanel(RectTransform rt, Sprite sprite, float borderScale)
    {
        if (rt == null) return;
        Off(rt, "Outline");
        Off(rt, "Gloss");
        Off(rt, "Rim");
        Off(rt, "Ring");

        var body = rt.Find("Body") as RectTransform;
        if (body == null) return;
        body.offsetMin = body.offsetMax = Vector2.zero;
        var im = body.GetComponent<Image>();
        if (im == null) return;
        im.sprite = sprite;
        im.type = Image.Type.Sliced;
        im.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, borderScale);
        im.color = Color.white;
    }

    // круглый бейдж: прежние слои выключаются, под иконкой появляется латунный медальон UiKit
    void StyleBadge(RectTransform badge, UiIcon icon)
    {
        Off(badge, "Outline");
        Off(badge, "Fill");
        Off(badge, "Gloss");
        Off(badge, "Ring");

        var ic = badge.Find("Icon") as RectTransform;
        if (badge.Find("KitMedallion") == null)
        {
            var med = UiKit.MakeImage(badge, "KitMedallion", UiKit.Medallion, false);
            var r = med.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.SetSiblingIndex(ic != null ? ic.GetSiblingIndex() : badge.childCount - 1);
        }
        if (ic != null)
        {
            var im = ic.GetComponent<Image>();
            if (im != null) { im.sprite = UiKit.Icon(icon); im.color = Color.white; im.preserveAspect = true; }
            float s = Mathf.Max(20f, badge.rect.width) * 0.62f;
            ic.sizeDelta = new Vector2(s, s);
        }
    }

    static void FitOneLine(TMP_Text t, float min, float max)
    {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.enableWordWrapping = false;
    }

    static void Off(Transform parent, string child)
    {
        var c = parent != null ? parent.Find(child) : null;
        if (c != null && c.gameObject.activeSelf) c.gameObject.SetActive(false);
    }
}
