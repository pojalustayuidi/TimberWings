// Assets/_Project/Scripts/Player/PlayerHeadHud.Build.cs (НЕ в Editor)
// Часть PlayerHeadHud: построение интерфейса над головой (Candy) и ритм-шкала.
//   Здесь: GetCanvas, NewUI, Stretch, Place, MakeImage, MakeBar, Txt, Build, BuildPack, LayoutSegments, ColorSegments, BuildChop, BuildRhythm, UpdateRhythmVisual.
//   В основном файле PlayerHeadHud.cs: API, состояние, жизненный цикл, SetChopMode, RefreshPack, LateUpdate, HeadTop.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHeadHud
{
    // ================================================================ создание UI
    static RectTransform GetCanvas()
    {
        if (canvasRoot != null) return canvasRoot;
        var go = new GameObject("PlayerHeadHud_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = 5;
        canvasCmp = go.GetComponent<Canvas>();
        canvasCmp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasCmp.sortingOrder = UiLayers.WorldTag;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRoot = (RectTransform)go.transform;
        return canvasRoot;
    }

    static RectTransform NewUI(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        for (int i = 0; i < comps.Length; i++) go.AddComponent(comps[i]);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r, float l, float b, float rt, float t)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
    }

    // прямоугольник от нижнего левого угла родителя
    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = Vector2.zero;
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    static Image MakeImage(RectTransform r, Sprite sprite, Color color, bool sliced)
    {
        var im = r.GetComponent<Image>();
        im.sprite = sprite;
        im.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        im.color = color;
        im.raycastTarget = false;
        return im;
    }

    static Image MakeBar(Transform parent, string name, float x, float y, float w, float h, Color track, Color fill, out Image fillImg)
    {
        var tr = NewUI(name, parent, typeof(Image));
        Place(tr, x, y, w, h);
        var t = MakeImage(tr, UiDraw.Pill, track, true);

        var fr = NewUI("Fill", tr, typeof(Image));
        Stretch(fr, 1.5f, 1.5f, 1.5f, 1.5f);
        fillImg = fr.GetComponent<Image>();
        fillImg.sprite = UiDraw.Pill;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0;
        fillImg.fillAmount = 0f;
        fillImg.color = fill;
        fillImg.raycastTarget = false;
        return t;
    }

    TMP_Text Txt(Transform parent, string name, float size, Color col, TextAlignmentOptions align)
    {
        var r = NewUI(name, parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        CandyTag.Font(t, fnt);                         // круглый шрифт Candy, а если его нет, шрифт HUD; обводки нет (плашка светлая)
        t.fontSize = size;
        t.color = col;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    void Build()
    {
        root = NewUI("PlayerHeadHud", GetCanvas(), typeof(CanvasGroup));
        root.anchorMin = root.anchorMax = Vector2.zero;       // позицию задаём в пикселях экрана
        root.pivot = new Vector2(0.5f, 0f);                    // опора снизу: появление таймера ничего не двигает
        root.sizeDelta = new Vector2(Mathf.Max(ChopW, PackW), Tail + PackH + Gap + ChopH + Gap + RhythmH);
        rootCg = root.GetComponent<CanvasGroup>();
        rootCg.blocksRaycasts = false;
        rootCg.interactable = false;
        rootCg.alpha = 0f;

        BuildPack();
        BuildChop();
        BuildRhythm();
    }

    void BuildPack()
    {
        // хвостик к голове: облачный треугольник Candy. Панель рисуется поверх и закрывает верхние пару пикселей
        var tl = NewUI("Tail", root, typeof(Image));
        tl.anchorMin = tl.anchorMax = new Vector2(0.5f, 0f);
        tl.pivot = new Vector2(0.5f, 0f);
        tl.anchoredPosition = Vector2.zero;
        tl.sizeDelta = new Vector2(26f, 26f * 22f / 36f);
        MakeImage(tl, CandyTag.Tail, Color.white, false);

        packRt = NewUI("Pack", root, typeof(Image));
        packRt.anchorMin = packRt.anchorMax = new Vector2(0.5f, 0f);
        packRt.pivot = new Vector2(0.5f, 0f);
        packRt.anchoredPosition = new Vector2(0f, Tail);
        packRt.sizeDelta = new Vector2(PackW, PackH);
        MakeImage(packRt, CandyTag.Panel, Color.white, true).pixelsPerUnitMultiplier = 1f / 0.4f;

        var fr = NewUI("Frame", packRt, typeof(Image));
        Stretch(fr, 0, 0, 0, 0);
        packFrame = MakeImage(fr, UiDraw.PillRing, FrameNormal, true);

        // медальон с рюкзаком: круглый, облачно-лавандовый, слегка выходит за левый край
        var med = NewUI("Medallion", packRt, typeof(Image));
        med.anchorMin = med.anchorMax = new Vector2(0f, 0.5f);
        med.pivot = new Vector2(0.5f, 0.5f);
        med.anchoredPosition = new Vector2(12f, 2f);
        med.sizeDelta = new Vector2(MedSize, MedSize);
        packMed = MakeImage(med, CandyTag.Medallion, Color.white, false);

        var ic = NewUI("Icon", med, typeof(Image));
        ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f);
        ic.pivot = new Vector2(0.5f, 0.5f);
        ic.anchoredPosition = Vector2.zero;
        ic.sizeDelta = new Vector2(MedSize * 0.58f, MedSize * 0.58f);
        packIcon = MakeImage(ic, CandyTag.Icon(UiIcon.Backpack), Color.white, false);
        packIcon.preserveAspect = true;

        packText = Txt(packRt, "Count", 46f, CandyTag.Ink, TextAlignmentOptions.MidlineLeft);
        Stretch(packText.rectTransform, SegX, SegY + SegH + 2f, 14f, 4f);   // слева, снизу (над сегментами), справа, сверху
        packText.enableAutoSizing = true;
        packText.fontSizeMin = 22f;
        packText.fontSizeMax = 46f;
        packText.fontStyle = FontStyles.Bold;

        segHost = NewUI("Segments", packRt);
        Place(segHost, SegX, SegY, PackW - SegX - 16f, SegH);
    }

    // сегменты по числу слотов: пересобираются только при смене вместимости
    void LayoutSegments()
    {
        if (segHost == null || segCap == cap) return;
        segCap = cap;

        float w = PackW - SegX - 16f;
        float gap = cap > 10 ? 2f : 3f;
        float cw = (w - gap * (cap - 1)) / cap;
        while (segs.Count < cap)
        {
            var r = NewUI("Seg" + segs.Count, segHost, typeof(Image));
            segs.Add(MakeImage(r, UiDraw.Pill, Track, false));
            segPop.Add(0f);
        }
        for (int i = 0; i < segs.Count; i++)
        {
            bool on = i < cap;
            segs[i].gameObject.SetActive(on);
            if (!on) continue;
            var rt = segs[i].rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cw, SegH);
            rt.anchoredPosition = new Vector2(i * (cw + gap) + cw * 0.5f, SegH * 0.5f);
        }
    }

    void ColorSegments(Color fill)
    {
        LayoutSegments();
        Color empty = Track;              // пустой сегмент: мягкая ямка Candy
        for (int i = 0; i < cap && i < segs.Count; i++) segs[i].color = i < wood ? fill : empty;
    }

    void BuildChop()
    {
        chopRt = NewUI("Chop", root, typeof(Image), typeof(CanvasGroup));
        chopRt.anchorMin = chopRt.anchorMax = new Vector2(0.5f, 0f);
        chopRt.pivot = new Vector2(0.5f, 0f);
        chopRt.anchoredPosition = new Vector2(0f, Tail + PackH + Gap);
        chopRt.sizeDelta = new Vector2(ChopW, ChopH);
        MakeImage(chopRt, CandyTag.Panel, Color.white, true).pixelsPerUnitMultiplier = 1f / 0.4f;
        chopCg = chopRt.GetComponent<CanvasGroup>();
        chopCg.blocksRaycasts = false;
        chopCg.interactable = false;
        chopCg.alpha = 0f;

        var fr = NewUI("Frame", chopRt, typeof(Image));
        Stretch(fr, 0, 0, 0, 0);
        chopFrame = MakeImage(fr, UiDraw.PillRing, FrameNormal, true);

        // верхняя строка: секунды слева, «+2 [бревно]» справа
        timeText = Txt(chopRt, "Time", 26f, CandyTag.Ink, TextAlignmentOptions.MidlineLeft);
        Stretch(timeText.rectTransform, 18f, 34f, 100f, 4f);
        timeText.enableAutoSizing = true;              // ритм-режим пишет сюда «Серия ×3»: текст сам уменьшается, чтобы влезть
        timeText.fontSizeMin = 16f;
        timeText.fontSizeMax = 26f;
        timeGo = timeText.gameObject;

        gainText = Txt(chopRt, "Gain", 26f, CandyTag.GoldText, TextAlignmentOptions.MidlineRight);
        Stretch(gainText.rectTransform, 110f, 34f, 48f, 4f);
        gainGo = gainText.gameObject;

        var gi = NewUI("GainIcon", chopRt, typeof(Image));
        gi.anchorMin = gi.anchorMax = new Vector2(1f, 1f);
        gi.pivot = new Vector2(1f, 1f);
        gi.anchoredPosition = new Vector2(-14f, -8f);
        gi.sizeDelta = new Vector2(26f, 26f);
        var gim = MakeImage(gi, CandyTag.Icon(UiIcon.Log), Color.white, false);
        gim.preserveAspect = true;
        gainIconGo = gi.gameObject;

        // «Готово!» вместо верхней строки
        doneText = Txt(chopRt, "Done", 30f, CandyTag.Good, TextAlignmentOptions.Center);
        Stretch(doneText.rectTransform, 10f, 28f, 10f, 2f);
        doneText.text = "Готово!";
        doneGo = doneText.gameObject;
        doneGo.SetActive(false);

        MakeBar(chopRt, "Bar", 18f, 12f, ChopW - 36f, 14f, Track, Amber, out chopFill);

        // тёплая вспышка поверх плашки
        var fl = NewUI("Flash", chopRt, typeof(Image));
        Stretch(fl, 0, 0, 0, 0);
        chopFlash = MakeImage(fl, UiDraw.Pill, new Color(1f, 1f, 1f, 0f), true);
    }

    void BuildRhythm()
    {
        rhythmRt = NewUI("Rhythm", root, typeof(Image), typeof(CanvasGroup));
        rhythmRt.anchorMin = rhythmRt.anchorMax = new Vector2(0.5f, 0f);
        rhythmRt.pivot = new Vector2(0.5f, 0f);
        rhythmRt.anchoredPosition = new Vector2(0f, Tail + PackH + Gap + ChopH + Gap);
        rhythmRt.sizeDelta = new Vector2(ChopW, RhythmH);
        MakeImage(rhythmRt, CandyTag.Panel, Color.white, true).pixelsPerUnitMultiplier = 1f / 0.4f;
        rhythmCg = rhythmRt.GetComponent<CanvasGroup>();
        rhythmCg.blocksRaycasts = false;
        rhythmCg.interactable = false;
        rhythmCg.alpha = 0f;

        // дорожка
        var tr = NewUI("Track", rhythmRt, typeof(Image));
        Place(tr, RhythmPad, 26f, RhythmTrackW, 18f);
        MakeImage(tr, UiDraw.Pill, Track, true);

        // свечение вокруг зелёной зоны: загорается, когда маркер внутри
        rzGlowRt = NewUI("Glow", rhythmRt, typeof(Image));
        Place(rzGlowRt, RhythmPad, 22f, RhythmTrackW * 0.5f, 26f);
        rzGlowImg = MakeImage(rzGlowRt, UiDraw.Pill, new Color(0.37f, 0.85f, 0.64f, 0f), true);

        // зелёная и золотая зоны: размеры задаёт SetRhythm
        rzGoodRt = NewUI("Good", rhythmRt, typeof(Image));
        Place(rzGoodRt, RhythmPad, 26f, RhythmTrackW * 0.5f, 18f);
        rzGoodImg = MakeImage(rzGoodRt, UiDraw.Pill, ZoneBase, true);

        rzPerfectRt = NewUI("Perfect", rhythmRt, typeof(Image));
        Place(rzPerfectRt, RhythmPad, 26f, RhythmTrackW * 0.2f, 18f);
        MakeImage(rzPerfectRt, UiDraw.Pill, UiCandy.Gold, true);

        // маркер
        rMarkerRt = NewUI("Marker", rhythmRt, typeof(Image));
        Place(rMarkerRt, RhythmPad, 20f, 8f, 30f);
        rMarkerRt.pivot = new Vector2(0.5f, 0.5f);
        rMarkerImg = MakeImage(rMarkerRt, UiDraw.Pill, CandyTag.Ink, false);      // индиго-маркер читается на светлой дорожке

        // рамка плашки: зелёная, когда маркер в зоне или после удачного нажатия
        var rf = NewUI("Frame", rhythmRt, typeof(Image));
        Stretch(rf, 0, 0, 0, 0);
        rFrameImg = MakeImage(rf, UiDraw.PillRing, new Color(0.37f, 0.85f, 0.64f, 0f), true);

        // подсказка внизу плашки (режим бонуса): «ПРОБЕЛ в зелёной зоне» / «ЖМИ ПРОБЕЛ!» / «Отлично! -0,3 с»
        rHintText = Txt(rhythmRt, "Hint", 18f, CandyTag.Ink, TextAlignmentOptions.Center);
        Stretch(rHintText.rectTransform, 8f, 3f, 8f, 34f);
        rHintText.enableAutoSizing = true;
        rHintText.fontSizeMin = 11f;
        rHintText.fontSizeMax = 18f;
        rHintText.text = "";

        // точки серии
        const float pip = 9f, pipGap = 6f;
        float startX = (ChopW - (5f * pip + 4f * pipGap)) * 0.5f;
        for (int i = 0; i < rPips.Length; i++)
        {
            var p = NewUI("Pip" + i, rhythmRt, typeof(Image));
            Place(p, startX + i * (pip + pipGap), 8f, pip, pip);
            rPips[i] = MakeImage(p, UiDraw.Pill, Track, false);
        }
    }

    void UpdateRhythmVisual()
    {
        float dt = Time.unscaledDeltaTime;

        // маркер в зелёной зоне и бить можно (не перезарядка): зона загорается, шкала пульсирует
        bool ready = Mathf.Abs(rMarker - 0.5f) <= rGoodHalf && rCooldown <= 0.001f;
        rInZone = Mathf.MoveTowards(rInZone, ready ? 1f : 0f, dt * 10f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f);

        float gw = RhythmTrackW * rGoodHalf * 2f;
        float lift = 3f * rInZone;                       // горящая зона чуть выше и толще дорожки
        rzGoodRt.anchoredPosition = new Vector2(RhythmPad + RhythmTrackW * (0.5f - rGoodHalf), 26f - lift);
        rzGoodRt.sizeDelta = new Vector2(gw, 18f + lift * 2f);
        rzGoodImg.color = Color.Lerp((Color)ZoneBase, ZoneHot, rInZone);

        float gl = 8f;
        rzGlowRt.anchoredPosition = new Vector2(RhythmPad + RhythmTrackW * (0.5f - rGoodHalf) - gl, 26f - gl);
        rzGlowRt.sizeDelta = new Vector2(gw + gl * 2f, 18f + gl * 2f);
        rzGlowImg.color = new Color(0.37f, 0.85f, 0.64f, rInZone * (0.30f + 0.25f * pulse));

        rHitFlash = Mathf.Max(0f, rHitFlash - dt * 2.5f);
        rFrameImg.color = new Color(0.37f, 0.85f, 0.64f, Mathf.Clamp01(Mathf.Max(rInZone * (0.55f + 0.35f * pulse), rHitFlash)));

        float pw = RhythmTrackW * rPerfectHalf * 2f;
        rzPerfectRt.anchoredPosition = new Vector2(RhythmPad + RhythmTrackW * (0.5f - rPerfectHalf), 26f);
        rzPerfectRt.sizeDelta = new Vector2(pw, 18f);

        rMarkerRt.anchoredPosition = new Vector2(RhythmPad + RhythmTrackW * rMarker, 35f);
        rMarkerRt.localScale = Vector3.one * (1f + 0.5f * rFlashK + 0.3f * rInZone);
        Color c = Color.Lerp(CandyTag.Ink, rFlashColor, rFlashK);
        c.a = Mathf.Lerp(1f, 0.45f, rCooldown);
        rMarkerImg.color = c;

        for (int i = 0; i < rPips.Length; i++)
        {
            rPips[i].enabled = !rBonusMode;              // в режиме бонуса серии нет: на этом месте подсказка
            rPips[i].color = i < rCombo ? UiCandy.Gold : Track;
        }

        // подсказка: результат нажатия > «ЖМИ ПРОБЕЛ!» (маркер в зоне) > «ПРОБЕЛ в зелёной зоне» (пока учим)
        string hint = "";
        if (rBonusMode)
        {
            if (Time.unscaledTime < rResultUntil) hint = rResultText;
            else if (rTeach) hint = rInZone > 0.5f ? HintGo : HintIdle;
        }
        if (hint != lastHint)
        {
            lastHint = hint;
            rHintText.text = hint;
        }
        bool go = rBonusMode && rTeach && rInZone > 0.5f && Time.unscaledTime >= rResultUntil;
        rHintText.rectTransform.localScale = Vector3.one * (go ? 1f + 0.12f * pulse : 1f);
    }

    // обычный вид (секунды + «+2») или «Готово!»
}
