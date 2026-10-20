// Assets/_Project/Scripts/Player/PlayerHeadHud.cs (НЕ в Editor)
// Всё, что висит над головой игрока, в одной колонке:
//
//        ┌──────────────────────┐
//        │ 1.8 с          +2 [] │   таймер рубки (виден только пока рубишь)
//        │ ▓▓▓▓▓▓▓░░░░░░░░░░░░  │   полоса: янтарная -> зелёная, в конце «Готово!»
//        └──────────────────────┘
//        ┌──────────────────────┐
//        │ ░░▒▒▓▓█▓▓▒▒░░  ◆ │   ритм-рубка: шкала с маркером (зелёная и золотая зоны) и точки серии
//        └──────────────────────┘   (виден только в ритм-режиме, над таймером)
//             ┌────────────┐
//             │ [] 4/6     │       рюкзак (виден всегда): иконка, «4/6», шкала
//             │ ▓▓▓▓░░     │       рамка: обычная, янтарная (почти полон), красная (полон)
//             └────────────┘
//
// Рюкзак стоит на одном месте, таймер появляется НАД ним, поэтому они не перекрываются и рюкзак не прыгает.
// Компонент создают BackpackTag и ChopProgress через PlayerHeadHud.Get(player), вешать вручную не нужно.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]      // после камеры: берём уже готовое положение
public class PlayerHeadHud : MonoBehaviour
{
    [Header("Положение")]
    public Vector3 headOffset = new Vector3(0f, 2.7f, 0f);
    public float screenMargin = 14f;          // отступ от края экрана (единицы канваса)
    [Tooltip("Камера «Свободная» (OrbitCamera): табличка вешается прямо над макушкой персонажа, зазор в единицах канваса")]
    public float orbitGap = 6f;

    [Header("Таймер рубки")]
    public float completeHold = 0.35f;        // сколько держится «Готово!» после последнего удара, с

    // размеры в единицах канваса (1920x1080)
    const float PackW = 252f, PackH = 74f;
    const float Tail = 11f;                          // хвостик-указатель под табличкой рюкзака: кончик касается головы
    const float MedSize = 86f;                       // медальон с рюкзаком выступает за левый край панели
    const float SegX = 76f, SegY = 10f, SegH = 11f;    // сегменты вместимости под числом
    const float ChopW = 224f, ChopH = 64f;
    const float RhythmH = 56f;                       // плашка ритм-шкалы над таймером
    const float RhythmPad = 18f, RhythmTrackW = ChopW - 36f;
    const float Gap = 8f;

    static readonly Color32 Panel = new Color32(0x14, 0x1C, 0x30, 250);
    static readonly Color32 FrameNormal = new Color32(0x3A, 0x4A, 0x70, 0);   // в покое рамка прозрачна: ободок уже в панели UiKit
    static readonly Color32 Amber = new Color32(0xF2, 0xA9, 0x3B, 255);
    static readonly Color32 Green = new Color32(0x6B, 0xCB, 0x5A, 255);
    static readonly Color32 Red = new Color32(0xFF, 0x8A, 0x7A, 255);
    static readonly Color32 Outline = new Color32(0x14, 0x1C, 0x30, 255);
    static readonly Color32 Track = new Color32(0x14, 0x1C, 0x30, 255);
    static readonly Color32 WeakGray = new Color32(0x8A, 0x94, 0xA0, 255);

    static RectTransform canvasRoot;
    static Canvas canvasCmp;
    static TMP_FontAsset fnt;

    // ---------------------------------------------------------------- UI
    RectTransform root, packRt, chopRt;
    CanvasGroup rootCg, chopCg;
    RectTransform rhythmRt, rzGoodRt, rzPerfectRt, rMarkerRt, rzGlowRt;
    Image rzGoodImg, rzGlowImg, rFrameImg;
    TMP_Text rHintText;
    CanvasGroup rhythmCg;
    Image rMarkerImg;
    readonly Image[] rPips = new Image[5];
    Image packFrame, packIcon, packMed, chopFill, chopFlash, chopFrame;
    RectTransform segHost;
    readonly System.Collections.Generic.List<Image> segs = new System.Collections.Generic.List<Image>(16);
    readonly System.Collections.Generic.List<float> segPop = new System.Collections.Generic.List<float>(16);
    int segCap = -1;
    float packBob;
    TMP_Text packText, timeText, gainText, doneText;
    GameObject timeGo, gainGo, gainIconGo, doneGo;
    Camera cam;

    // ---------------------------------------------------------------- состояние рюкзака
    int wood, cap = 4;
    float revealHide;               // 0..1: плавно прячем весь HUD над головой на время показа стадии здания (BuildReveal), чтобы не закрывал вид
    float packFillTarget, packFillShown;
    float packPunch, packPunchAmount;

    // ---------------------------------------------------------------- состояние таймера
    bool chopWant, chopComplete, chopPending;
    float chopHideAt;
    float chopShown;                  // 0..1 видимость плашки
    float chopProgTarget, chopProgShown;
    float chopFlashK, chopPunch, chopPunchAmount;
    int lastTenths = -1;
    int gainShown = -1;

    // ---------------------------------------------------------------- состояние ритм-шкалы
    bool rhythmWant;
    float rhythmShown;
    float rMarker, rGoodHalf = 0.25f, rPerfectHalf = 0.1f, rCooldown;
    int rCombo;
    float rFlashK;
    Color rFlashColor = Color.white;
    string lastLabel;

    // бонус «зажми E + Пробел»: подсветка зоны, подсказка и результат нажатия
    bool rBonusMode;                  // шкала показывает бонус (одна зелёная зона), а не ритм (зелёная + золотая)
    bool rTeach;                      // показывать обучающую подсказку про Пробел
    float rInZone;                    // 0..1: маркер в зелёной зоне и бить можно (плавно, для подсветки)
    float rHitFlash;                  // 0..1: зелёная рамка после удачного нажатия
    float rResultUntil;
    string rResultText = "";
    string lastHint;

    static readonly Color32 ZoneBase = new Color32(0x6B, 0xCB, 0x5A, 210);
    static readonly Color ZoneHot = new Color(0.66f, 1f, 0.52f, 1f);
    const string HintIdle = "<color=#F2A93B>ПРОБЕЛ</color> в зелёной зоне";
    const string HintGo = "<color=#6BCB5A>ЖМИ</color> <color=#F2A93B>ПРОБЕЛ</color>!";

    public static PlayerHeadHud Get(GameObject host)
    {
        var h = host.GetComponent<PlayerHeadHud>();
        if (h == null) h = host.AddComponent<PlayerHeadHud>();
        return h;
    }

    // ================================================================ API: рюкзак
    public void SetPackIcon(Sprite s)
    {
        if (packIcon == null) return;
        packIcon.sprite = s != null ? s : UiKit.Icon(UiIcon.Backpack);
    }

    public void SetBackpack(int carried, int capacity, bool grew)
    {
        wood = Mathf.Max(0, carried);
        cap = Mathf.Max(1, capacity);
        packFillTarget = Mathf.Clamp01(wood / (float)cap);
        RefreshPack();
        if (grew)
        {
            PunchPack(0.2f);
            int idx = Mathf.Min(wood, segPop.Count) - 1;      // только что заполненный сегмент выпрыгивает
            if (idx >= 0) segPop[idx] = 1f;
        }
    }

    public void PunchPack(float amount)
    {
        packPunchAmount = amount;
        packPunch = 1f;
    }

    // ================================================================ API: таймер рубки
    // progress 0..1, secondsLeft: сколько секунд до конца, gain: сколько брёвен даст дерево
    public void ShowChop(float progress, float secondsLeft, int gain, int of = 0)
    {
        progress = Mathf.Clamp01(progress);

        // свежий запуск или новое дерево сразу после прошлого: полоса не «отматывается» назад
        if (!chopWant || chopComplete) chopProgShown = progress;

        chopWant = true;
        chopComplete = false;
        chopPending = false;
        chopProgTarget = progress;
        rhythmWant = false;
        lastLabel = null;

        SetChopMode(false);

        int tenths = Mathf.Max(0, Mathf.CeilToInt(secondsLeft * 10f));
        if (tenths != lastTenths)
        {
            lastTenths = tenths;
            timeText.text = (tenths / 10) + "," + (tenths % 10) + " с";
        }
        SetGain(gain, of);
    }

    // «+2» или «+2/4» (янтарным, если дерево даёт больше, чем влезает в рюкзак). Строка собирается только при смене чисел
    void SetGain(int gain, int of)
    {
        if (of < gain) of = gain;
        int key = gain * 1000 + of;
        if (key == gainShown) return;
        gainShown = key;
        bool partial = of > gain;
        gainText.text = partial ? "+" + gain + "/" + of : "+" + gain;
        gainText.color = partial ? Red : Amber;
    }

    // ритм-рубка: та же плашка, но вместо секунд короткая подпись («В ритм!», «Серия ×3»), сверху появляется шкала с маркером.
    // Вызывать каждый кадр, пока игрок в стойке. label лучше кэшировать: строка на каждый кадр = мусор для GC.
    public void ShowChopRhythm(float progress, int gain, string label, int of = 0)
    {
        progress = Mathf.Clamp01(progress);
        if (!chopWant || chopComplete) chopProgShown = progress;

        chopWant = true;
        chopComplete = false;
        chopPending = false;
        chopProgTarget = progress;
        rhythmWant = true;
        lastTenths = -1;

        SetChopMode(false);

        if (label != lastLabel)
        {
            lastLabel = label;
            timeText.text = label;
        }
        SetGain(gain, of);
    }

    // бонус рубки (зажми E + Пробел): шкала с одной зелёной зоной над обычным таймером. Вызывать КАЖДЫЙ кадр после ShowChop
    // (ShowChop гасит шкалу, этот вызов включает обратно). on = false: шкала не нужна (бонус выключен в настройках).
    // teach = true: на шкале висит подсказка «ПРОБЕЛ в зелёной зоне», а когда маркер в зоне, она меняется на «ЖМИ ПРОБЕЛ!»
    public void SetChopBonus(bool on, float marker01, float zoneHalf, float cooldown01, bool teach = false)
    {
        rhythmWant = on;
        if (!on) return;
        SetRhythm(marker01, zoneHalf, 0f, 0, cooldown01);   // золотой зоны нет: ширина 0
        rBonusMode = true;
        rTeach = teach;
    }

    // итог нажатия Пробела: «Отлично! -0,3 с» (зелёная рамка) или «Мимо, ещё раз» (без наказания). Подпись живёт меньше секунды
    public void ShowBonusResult(bool hit, float savedSeconds)
    {
        rResultUntil = Time.unscaledTime + (hit ? 0.9f : 0.6f);
        if (hit)
        {
            int tenths = Mathf.Max(1, Mathf.RoundToInt(savedSeconds * 10f));
            rResultText = "<color=#6BCB5A>Отлично!</color> -" + (tenths / 10) + "," + (tenths % 10) + " с";
            rHitFlash = 1f;
        }
        else
        {
            rResultText = "<color=#AEB9D6>Мимо, ещё раз</color>";
        }
    }

    // положение маркера 0..1, половины ширины зон (доля шкалы), серия, перезарядка 0..1 (маркер бледнеет, пока бить нельзя)
    public void SetRhythm(float marker01, float goodHalf, float perfectHalf, int combo, float cooldown01)
    {
        rMarker = Mathf.Clamp01(marker01);
        rGoodHalf = goodHalf;
        rPerfectHalf = perfectHalf;
        rCombo = combo;
        rCooldown = Mathf.Clamp01(cooldown01);
        rBonusMode = false;      // SetChopBonus включит обратно
    }

    // вспышка маркера в момент нажатия: 0 слабый (серый), 1 хороший (зелёный), 2 идеальный (золотой + хлопок плашки)
    public void RhythmFlash(int quality)
    {
        rFlashK = 1f;
        rFlashColor = quality >= 2 ? (Color)Amber : quality == 1 ? (Color)Green : (Color)WeakGray;
        if (quality >= 2 && chopWant)
        {
            chopPunchAmount = 0.08f;
            chopPunch = 1f;
        }
    }

    // дерево срублено: полоса полная, зелёная, «Готово!» и хлопок
    public void CompleteChop()
    {
        if (!chopWant) return;
        chopComplete = true;
        chopProgTarget = 1f;
        chopFlashK = 1f;
        chopPunchAmount = 0.12f;
        chopPunch = 1f;
        chopHideAt = Time.unscaledTime + completeHold;
        SetChopMode(true);
    }

    public void HideChop()
    {
        if (!chopWant) return;
        if (chopComplete && Time.unscaledTime < chopHideAt)
        {
            chopPending = true;      // дай доиграть «Готово!»
            return;
        }
        chopWant = false;
        chopPending = false;
    }

    // ================================================================ жизненный цикл
    void Awake()
    {
        TryGetFont();
        Build();
    }

    void Start()
    {
        if (fnt == null && TryGetFont()) ApplyFont();
        RefreshPack();
    }

    void OnEnable() { if (root != null) root.gameObject.SetActive(true); }
    void OnDisable() { if (root != null) root.gameObject.SetActive(false); }
    void OnDestroy() { if (root != null) Destroy(root.gameObject); }

    static bool TryGetFont()
    {
        if (fnt != null) return true;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null)
            fnt = UIManager.Instance.objectiveText.font;
        return fnt != null;
    }

    void ApplyFont()
    {
        if (root == null || fnt == null) return;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) t.font = fnt;
    }

    // ================================================================ создание UI
    static RectTransform GetCanvas()
    {
        if (canvasRoot != null) return canvasRoot;
        var go = new GameObject("PlayerHeadHud_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = 5;
        canvasCmp = go.GetComponent<Canvas>();
        canvasCmp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasCmp.sortingOrder = 5;                     // ниже HUD (10) и всплывающих наград (20)
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
        if (fnt != null) t.font = fnt;                 // сначала шрифт, потом обводка
        t.fontSize = size;
        t.color = col;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.richText = true;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.outlineWidth = 0.18f;
        t.outlineColor = Outline;
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
        // хвостик к голове: два ромба (латунный ободок и тёмная заливка). Панель рисуется поверх и закрывает верхние половинки
        MakeDiamond("TailRim", 17f, Tail + 0.5f, UiKit.Brass);
        MakeDiamond("TailFill", 12f, Tail + 2.6f, (Color)Panel);

        packRt = NewUI("Pack", root, typeof(Image));
        packRt.anchorMin = packRt.anchorMax = new Vector2(0.5f, 0f);
        packRt.pivot = new Vector2(0.5f, 0f);
        packRt.anchoredPosition = new Vector2(0f, Tail);
        packRt.sizeDelta = new Vector2(PackW, PackH);
        MakeImage(packRt, UiKit.Panel, Color.white, true).pixelsPerUnitMultiplier = 1f / 0.4f;

        var fr = NewUI("Frame", packRt, typeof(Image));
        Stretch(fr, 0, 0, 0, 0);
        packFrame = MakeImage(fr, UiDraw.PillRing, FrameNormal, true);

        // медальон с рюкзаком: круглый, латунный, слегка выходит за левый край
        var med = NewUI("Medallion", packRt, typeof(Image));
        med.anchorMin = med.anchorMax = new Vector2(0f, 0.5f);
        med.pivot = new Vector2(0.5f, 0.5f);
        med.anchoredPosition = new Vector2(12f, 2f);
        med.sizeDelta = new Vector2(MedSize, MedSize);
        packMed = MakeImage(med, UiKit.Medallion, Color.white, false);

        var ic = NewUI("Icon", med, typeof(Image));
        ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f);
        ic.pivot = new Vector2(0.5f, 0.5f);
        ic.anchoredPosition = Vector2.zero;
        ic.sizeDelta = new Vector2(MedSize * 0.58f, MedSize * 0.58f);
        packIcon = MakeImage(ic, UiKit.Icon(UiIcon.Backpack), Color.white, false);
        packIcon.preserveAspect = true;

        packText = Txt(packRt, "Count", 46f, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(packText.rectTransform, SegX, SegY + SegH + 2f, 14f, 4f);   // слева, снизу (над сегментами), справа, сверху
        packText.enableAutoSizing = true;
        packText.fontSizeMin = 22f;
        packText.fontSizeMax = 46f;
        packText.fontStyle = FontStyles.Bold;
        packText.outlineWidth = 0.28f;                      // жирная обводка: число читается на траве, песке и ночью
        packText.outlineColor = Outline;

        segHost = NewUI("Segments", packRt);
        Place(segHost, SegX, SegY, PackW - SegX - 16f, SegH);
    }

    void MakeDiamond(string name, float size, float centerY, Color color)
    {
        var r = NewUI(name, root, typeof(Image));
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, centerY);
        r.sizeDelta = new Vector2(size, size);
        r.localRotation = Quaternion.Euler(0f, 0f, 45f);
        MakeImage(r, null, color, false);
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
        Color empty = new Color(0.08f, 0.11f, 0.19f, 0.95f);
        for (int i = 0; i < cap && i < segs.Count; i++) segs[i].color = i < wood ? fill : empty;
    }

    void BuildChop()
    {
        chopRt = NewUI("Chop", root, typeof(Image), typeof(CanvasGroup));
        chopRt.anchorMin = chopRt.anchorMax = new Vector2(0.5f, 0f);
        chopRt.pivot = new Vector2(0.5f, 0f);
        chopRt.anchoredPosition = new Vector2(0f, Tail + PackH + Gap);
        chopRt.sizeDelta = new Vector2(ChopW, ChopH);
        MakeImage(chopRt, UiKit.Panel, Color.white, true).pixelsPerUnitMultiplier = 1f / 0.4f;
        chopCg = chopRt.GetComponent<CanvasGroup>();
        chopCg.blocksRaycasts = false;
        chopCg.interactable = false;
        chopCg.alpha = 0f;

        var fr = NewUI("Frame", chopRt, typeof(Image));
        Stretch(fr, 0, 0, 0, 0);
        chopFrame = MakeImage(fr, UiDraw.PillRing, FrameNormal, true);

        // верхняя строка: секунды слева, «+2 [бревно]» справа
        timeText = Txt(chopRt, "Time", 26f, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(timeText.rectTransform, 18f, 34f, 100f, 4f);
        timeText.enableAutoSizing = true;              // ритм-режим пишет сюда «Серия ×3»: текст сам уменьшается, чтобы влезть
        timeText.fontSizeMin = 16f;
        timeText.fontSizeMax = 26f;
        timeGo = timeText.gameObject;

        gainText = Txt(chopRt, "Gain", 26f, Amber, TextAlignmentOptions.MidlineRight);
        Stretch(gainText.rectTransform, 110f, 34f, 48f, 4f);
        gainGo = gainText.gameObject;

        var gi = NewUI("GainIcon", chopRt, typeof(Image));
        gi.anchorMin = gi.anchorMax = new Vector2(1f, 1f);
        gi.pivot = new Vector2(1f, 1f);
        gi.anchoredPosition = new Vector2(-14f, -8f);
        gi.sizeDelta = new Vector2(26f, 26f);
        var gim = MakeImage(gi, UiKit.Icon(UiIcon.Log), Color.white, false);
        gim.preserveAspect = true;
        gainIconGo = gi.gameObject;

        // «Готово!» вместо верхней строки
        doneText = Txt(chopRt, "Done", 30f, Green, TextAlignmentOptions.Center);
        Stretch(doneText.rectTransform, 10f, 28f, 10f, 2f);
        doneText.text = "Готово!";
        doneGo = doneText.gameObject;
        doneGo.SetActive(false);

        MakeBar(chopRt, "Bar", 18f, 12f, ChopW - 36f, 14f, Track, Amber, out chopFill);

        // белая вспышка поверх плашки
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
        MakeImage(rhythmRt, UiKit.Panel, Color.white, true).pixelsPerUnitMultiplier = 1f / 0.4f;
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
        rzGlowImg = MakeImage(rzGlowRt, UiDraw.Pill, new Color(0.42f, 0.80f, 0.35f, 0f), true);

        // зелёная и золотая зоны: размеры задаёт SetRhythm
        rzGoodRt = NewUI("Good", rhythmRt, typeof(Image));
        Place(rzGoodRt, RhythmPad, 26f, RhythmTrackW * 0.5f, 18f);
        rzGoodImg = MakeImage(rzGoodRt, UiDraw.Pill, ZoneBase, true);

        rzPerfectRt = NewUI("Perfect", rhythmRt, typeof(Image));
        Place(rzPerfectRt, RhythmPad, 26f, RhythmTrackW * 0.2f, 18f);
        MakeImage(rzPerfectRt, UiDraw.Pill, Amber, true);

        // маркер
        rMarkerRt = NewUI("Marker", rhythmRt, typeof(Image));
        Place(rMarkerRt, RhythmPad, 20f, 8f, 30f);
        rMarkerRt.pivot = new Vector2(0.5f, 0.5f);
        rMarkerImg = MakeImage(rMarkerRt, UiDraw.Pill, Color.white, false);

        // рамка плашки: зелёная, когда маркер в зоне или после удачного нажатия
        var rf = NewUI("Frame", rhythmRt, typeof(Image));
        Stretch(rf, 0, 0, 0, 0);
        rFrameImg = MakeImage(rf, UiDraw.PillRing, new Color(0.42f, 0.80f, 0.35f, 0f), true);

        // подсказка внизу плашки (режим бонуса): «ПРОБЕЛ в зелёной зоне» / «ЖМИ ПРОБЕЛ!» / «Отлично! -0,3 с»
        rHintText = Txt(rhythmRt, "Hint", 18f, Color.white, TextAlignmentOptions.Center);
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
        rzGlowImg.color = new Color(0.42f, 0.80f, 0.35f, rInZone * (0.30f + 0.25f * pulse));

        rHitFlash = Mathf.Max(0f, rHitFlash - dt * 2.5f);
        rFrameImg.color = new Color(0.42f, 0.80f, 0.35f, Mathf.Clamp01(Mathf.Max(rInZone * (0.55f + 0.35f * pulse), rHitFlash)));

        float pw = RhythmTrackW * rPerfectHalf * 2f;
        rzPerfectRt.anchoredPosition = new Vector2(RhythmPad + RhythmTrackW * (0.5f - rPerfectHalf), 26f);
        rzPerfectRt.sizeDelta = new Vector2(pw, 18f);

        rMarkerRt.anchoredPosition = new Vector2(RhythmPad + RhythmTrackW * rMarker, 35f);
        rMarkerRt.localScale = Vector3.one * (1f + 0.5f * rFlashK + 0.3f * rInZone);
        Color c = Color.Lerp(Color.white, rFlashColor, rFlashK);
        c.a = Mathf.Lerp(1f, 0.45f, rCooldown);
        rMarkerImg.color = c;

        for (int i = 0; i < rPips.Length; i++)
        {
            rPips[i].enabled = !rBonusMode;              // в режиме бонуса серии нет: на этом месте подсказка
            rPips[i].color = i < rCombo ? (Color)Amber : (Color)Track;
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
    void SetChopMode(bool done)
    {
        if (doneGo.activeSelf != done) doneGo.SetActive(done);
        if (timeGo.activeSelf == done) timeGo.SetActive(!done);
        if (gainGo.activeSelf == done) gainGo.SetActive(!done);
        if (gainIconGo.activeSelf == done) gainIconGo.SetActive(!done);
    }

    // ================================================================ рюкзак: текст и цвета
    void RefreshPack()
    {
        if (packText == null) return;

        bool full = wood >= cap;
        bool warm = !full && wood / (float)cap >= 0.75f;

        string num = wood.ToString();
        string capS = "<color=#AEB9D6>/" + cap + "</color>";
        Color32 frame = FrameNormal, fill = Green;

        if (full)
        {
            packText.text = "<color=#FF8A7A>" + num + "</color>" + capS + " <size=60%><color=#FF8A7A>полон</color></size>";
            frame = Red; fill = Red;
        }
        else if (warm)
        {
            packText.text = "<color=#F2A93B>" + num + "</color>" + capS;
            frame = Amber; fill = Amber;
        }
        else
        {
            packText.text = num + capS;
        }
        packFrame.color = frame;
        ColorSegments(fill);
        if (packMed != null) packMed.color = full ? new Color(1f, 0.72f, 0.66f, 1f) : (warm ? new Color(1f, 0.92f, 0.76f, 1f) : Color.white);
    }

    // ================================================================ позиция и анимация
    void LateUpdate()
    {
        if (root == null) return;

        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        if (chopPending && now >= chopHideAt) { chopWant = false; chopPending = false; }

        // --- анимации
        chopShown = Mathf.MoveTowards(chopShown, chopWant ? 1f : 0f, dt / 0.18f);
        float k = 1f - Mathf.Exp(-14f * dt);
        chopProgShown = Mathf.Lerp(chopProgShown, chopProgTarget, k);
        packFillShown = Mathf.Lerp(packFillShown, packFillTarget, 1f - Mathf.Exp(-10f * dt));
        if (chopFlashK > 0f) chopFlashK = Mathf.Max(0f, chopFlashK - dt * 3.3f);

        // ритм-плашка живёт, пока идёт рубка (после «Готово!» уходит вместе с таймером)
        bool rhythmOn = rhythmWant && chopWant && !chopComplete;
        rhythmShown = Mathf.MoveTowards(rhythmShown, rhythmOn ? 1f : 0f, dt / 0.18f);
        if (rFlashK > 0f) rFlashK = Mathf.Max(0f, rFlashK - dt * 4f);
        if (rhythmShown > 0.001f) UpdateRhythmVisual();
        rhythmRt.localScale = Vector3.one * (rhythmShown > 0.001f ? UiDraw.EaseOutBack(rhythmShown) : 0f);
        rhythmCg.alpha = Mathf.Clamp01(rhythmShown * 1.6f);
        if (chopPunch > 0f) chopPunch = Mathf.Max(0f, chopPunch - dt / 0.3f);
        if (packPunch > 0f) packPunch = Mathf.Max(0f, packPunch - dt / 0.3f);

        chopFill.fillAmount = chopProgShown;
        chopFill.color = Color.Lerp(Amber, Green, chopProgShown);
        chopFrame.color = Color.Lerp(FrameNormal, Green, chopComplete ? 1f : 0f);
        chopFlash.color = new Color(1f, 1f, 1f, chopFlashK * 0.55f);

        // сегменты: свежезаполненный выпрыгивает
        for (int i = 0; i < segPop.Count; i++)
        {
            if (segPop[i] <= 0f) { if (segs[i].rectTransform.localScale.x != 1f) segs[i].rectTransform.localScale = Vector3.one; continue; }
            segPop[i] = Mathf.Max(0f, segPop[i] - dt / 0.3f);
            float ps = 1f + 0.9f * Mathf.Sin((1f - segPop[i]) * Mathf.PI);
            segs[i].rectTransform.localScale = new Vector3(1f, ps, 1f);
        }

        // лёгкое покачивание таблички; при полном рюкзаке красная рамка дышит
        packBob += dt;
        packRt.anchoredPosition = new Vector2(0f, Tail + Mathf.Sin(packBob * 2.2f) * 1.6f);
        if (wood >= cap)
        {
            Color fc = packFrame.color;
            fc.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(packBob * 6f));
            packFrame.color = fc;
        }

        float packScale = 1f + packPunchAmount * Mathf.Sin((1f - packPunch) * Mathf.PI) * (packPunch > 0f ? 1f : 0f);
        packRt.localScale = Vector3.one * packScale;

        float chopScale = chopShown > 0.001f ? UiDraw.EaseOutBack(chopShown) : 0f;
        chopScale *= 1f + chopPunchAmount * Mathf.Sin((1f - chopPunch) * Mathf.PI) * (chopPunch > 0f ? 1f : 0f);
        chopRt.localScale = Vector3.one * chopScale;
        chopCg.alpha = Mathf.Clamp01(chopShown * 1.6f);

        // --- положение на экране
        if (cam == null) cam = Camera.main;
        if (cam == null) { rootCg.alpha = 0f; return; }

        // свободная камера: точка прямо над макушкой (высота модели), а не фиксированные 2.7 м, которые на экране «улетают» вверх
        Vector3 wp = transform.position + (OrbitCamera.Active ? Vector3.up * HeadTop() : headOffset);
        Vector3 sp;
        if (!CameraFollow.TryWorldToScreenStable(wp, out sp)) sp = cam.WorldToScreenPoint(wp);
        if (sp.z <= 0.1f) { rootCg.alpha = 0f; return; }

        // пока камера облетает здание (показ стадии), рюкзак и полоски рубки над игроком уходят в прозрачность и возвращаются после
        revealHide = Mathf.MoveTowards(revealHide, BuildingController.AnyRevealing ? 1f : 0f, dt / 0.25f);
        rootCg.alpha = 1f - revealHide;

        float scale = canvasCmp != null ? Mathf.Max(0.01f, canvasCmp.scaleFactor) : 1f;
        if (OrbitCamera.Active) sp.y += orbitGap * scale;
        float w = Mathf.Max(ChopW, PackW) * scale;
        float rhythmExtra = rhythmShown > 0.01f ? Gap + RhythmH : 0f;
        float h = (Tail + (chopShown > 0.01f ? PackH + Gap + ChopH + rhythmExtra : PackH)) * scale;
        float m = screenMargin * scale;

        float x = Mathf.Clamp(sp.x, w * 0.5f + m, Mathf.Max(w * 0.5f + m, Screen.width - w * 0.5f - m));
        float y = Mathf.Clamp(sp.y, m, Mathf.Max(m, Screen.height - h - m));
        root.position = new Vector3(x, y, 0f);
    }

    // высота макушки над ногами по рендерерам персонажа (считаем один раз; рюкзак, меч и т. п. входят)
    float headTop = -1f;
    float HeadTop()
    {
        if (headTop > 0f) return headTop;
        float top = float.MinValue;
        var rs = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null || !r.enabled) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.GetComponent<TMP_Text>() != null) continue;      // 3D-текст над головой макушкой не считаем
            top = Mathf.Max(top, r.bounds.max.y);
        }
        float h = top > float.MinValue ? top - transform.position.y : headOffset.y - 0.7f;
        headTop = Mathf.Clamp(h + 0.05f, 1f, headOffset.y);          // не выше прежней точки и не ниже метра
        return headTop;
    }
}
