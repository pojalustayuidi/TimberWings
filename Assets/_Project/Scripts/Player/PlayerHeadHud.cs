// Assets/_Project/Scripts/Player/PlayerHeadHud.cs (НЕ в Editor)
// Всё, что висит над головой игрока, в одной колонке (Candy: светлые облачные плашки, индиго-текст, мятные и персиковые полосы).
// Файл разбит на части (partial): здесь API, состояние и жизненный цикл, ниже в основном файле рюкзак и LateUpdate; построение UI и ритм-шкала в PlayerHeadHud.Build.cs.
//
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
public partial class PlayerHeadHud : MonoBehaviour
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
    const float Tail = 14f;                          // хвостик-указатель под табличкой рюкзака: кончик касается головы
    const float MedSize = 86f;                       // медальон с рюкзаком выступает за левый край панели
    const float SegX = 76f, SegY = 12f, SegH = 11f;    // сегменты вместимости под числом
    const float ChopW = 224f, ChopH = 64f;
    const float RhythmH = 56f;                       // плашка ритм-шкалы над таймером
    const float RhythmPad = 18f, RhythmTrackW = ChopW - 36f;
    const float Gap = 8f;

    static readonly Color32 FrameNormal = new Color32(0xBD, 0xC2, 0xFA, 0);   // в покое рамка прозрачна: ободок уже в панели Candy
    static readonly Color Amber = UiCandy.PeachDeep;     // «почти полон»
    static readonly Color Green = UiCandy.Mint;          // «в зелёной зоне», готово
    static readonly Color Red = CandyTag.CoralFill;      // «полон»
    static readonly Color Track = CandyTag.Track;        // пустая дорожка и пустые сегменты
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

    static readonly Color32 ZoneBase = new Color32(0x5F, 0xD9, 0xA2, 235);
    static readonly Color ZoneHot = new Color(0.55f, 1f, 0.78f, 1f);
    const string HintIdle = "<color=#9A5F00>ПРОБЕЛ</color> в зелёной зоне";
    const string HintGo = "<color=#2A9573>ЖМИ</color> <color=#9A5F00>ПРОБЕЛ</color>!";

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
        packIcon.sprite = s != null ? s : CandyTag.Icon(UiIcon.Backpack);
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
        gainText.color = partial ? CandyTag.Danger : CandyTag.GoldText;
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
            rResultText = "<color=#2A9573>Отлично!</color> -" + (tenths / 10) + "," + (tenths % 10) + " с";
            rHitFlash = 1f;
        }
        else
        {
            rResultText = "<color=#5E6A96>Мимо, ещё раз</color>";
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
        if (root == null) return;
        var f = CandyTag.BestFont(fnt);        // круглый шрифт Candy главнее шрифта HUD
        if (f == null) return;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) t.font = f;
    }

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
        string capS = "<color=#5E6A96>/" + cap + "</color>";
        Color frame = FrameNormal, fill = Green;

        if (full)
        {
            packText.text = "<color=#C8365C>" + num + "</color>" + capS + " <size=60%><color=#C8365C>полон</color></size>";
            frame = Red; fill = Red;
        }
        else if (warm)
        {
            packText.text = "<color=#9A5F00>" + num + "</color>" + capS;
            frame = Amber; fill = Amber;
        }
        else
        {
            packText.text = num + capS;       // число индиго (цвет текста), «/6» приглушённое
        }
        packFrame.color = frame;
        ColorSegments(fill);
        if (packMed != null) packMed.color = full ? new Color(1f, 0.78f, 0.74f, 1f) : (warm ? new Color(1f, 0.93f, 0.78f, 1f) : Color.white);
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
        chopFlash.color = new Color(1f, 0.93f, 0.55f, chopFlashK * 0.6f);     // тёплая вспышка (белая на светлой плашке не видна)

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
