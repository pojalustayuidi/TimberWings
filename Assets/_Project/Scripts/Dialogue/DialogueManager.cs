// Assets/_Project/Scripts/Dialogue/DialogueManager.cs (НЕ в Editor)
// Показывает реплики: пузырь над говорящим (до 120 символов, печатается по буквам,
// исчезает через 4-5 с или по E) и субтитры внизу экрана (по умолчанию включены).
// Создаётся сам при первой реплике, на сцену вешать ничего не нужно.
//
// Из кода:
//   DialogueManager.Say(speaker, "Текст");
//   DialogueManager.SayAs("Лео", "Тачка уже полна. Надо донести до лагеря.");
//   DialogueManager.SubtitlesEnabled = false;   // переключатель для меню настроек
//
// Правило: реплика даёт эмоцию и сюжет, но никогда не заменяет инструкцию. Инструкции живут в панели задания.
//
// Управление репликой (E или кнопка действия):
//   тап (отпустил раньше 0.6 с)  первый раз дописывает текст, второй раз убирает реплику
//   удержание 0.6 с              пропускает ВЕСЬ разговор: вокруг кнопки заполняется кольцо, пузырь плавно уходит,
//                                голос затухает, на экране «Разговор пропущен». События реплик всё равно срабатывают,
//                                тексты попадают в DialogueLog. Внизу пузыря подсказка: первые реплики полная, потом короткая.
// Озвучка: если у реплики нет своего клипа, ищется Resources/Voice/<id>.mp3 (VoiceBank, id см. VoiceId), иначе бормотание.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    class Req
    {
        public Speaker who;
        public string text;
        public AudioClip clip;
        public Action onDone;
        public float dur;
    }

    static DialogueManager inst;

    public static DialogueManager Instance
    {
        get
        {
            if (inst == null && Application.isPlaying)
            {
                var go = new GameObject("DialogueManager");
                inst = go.AddComponent<DialogueManager>();
            }
            return inst;
        }
    }

    const string PrefSubs = "BuilderSim.Subtitles";
    public static bool SubtitlesEnabled
    {
        get { return PlayerPrefs.GetInt(PrefSubs, 1) == 1; }
        set { PlayerPrefs.SetInt(PrefSubs, value ? 1 : 0); }
    }

    public static bool IsBusy { get { return inst != null && (inst.cur != null || inst.queue.Count > 0); } }

    [Header("Настройки")]
    public KeyCode skipKey = KeyCode.E;
    public int maxChars = 120;
    public float bubbleWidth = 460f;
    public float typeSpeed = 60f;            // букв в секунду
    public float minShowBeforeSkip = 0.6f;   // чтобы случайное E не смахнуло реплику сразу

    [Header("Пропуск разговора")]
    public float skipAllHoldTime = 0.6f;     // столько секунд держать E, чтобы пропустить весь разговор
    public float holdShowDelay = 0.2f;       // кольцо появляется не сразу, обычный тап его не мигает
    public bool showSkipHint = true;
    public int fullHintLines = 6;            // сколько первых реплик в игре показывают полную подсказку

    // ------------------------------------------------------------ API
    public static void Say(Speaker who, string text, AudioClip clip = null, Action onDone = null)
    {
        var m = Instance;
        if (m == null) { if (onDone != null) onDone(); return; }
        m.Enqueue(who, text, clip, onDone);
    }

    public static void SayAs(string speakerName, string text, AudioClip clip = null, Action onDone = null)
    {
        Say(Speaker.Find(speakerName), text, clip, onDone);
    }

    // пропуск разговора целиком (удержание E). DialogueSequence смотрит этот флаг между репликами
    public static bool SkipAllRequested { get; private set; }
    // пока игрок не отпустил E после пропуска, хаб не отдаёт её рубке (иначе удержание сразу начало бы рубить дерево)
    public static bool BlocksUntilRelease { get; private set; }
    public static void BeginConversation() { SkipAllRequested = false; }
    public static void EndConversation() { SkipAllRequested = false; }
    public static void RequestSkipAll() { if (inst != null) inst.DoSkipAll(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { SkipAllRequested = false; BlocksUntilRelease = false; }

    public static void Clear()
    {
        if (inst == null) return;
        inst.queue.Clear();
        if (inst.cur != null) inst.Finish(true);
    }

    // ------------------------------------------------------------ состояние
    readonly Queue<Req> queue = new Queue<Req>();
    Req cur;
    float t, typed, shown, gap;
    int total;
    bool typedDone;

    // пропуск и подсказка
    bool pressing;
    float pressT, holdProgress, outTime = 0.3f, noteT, hintAlpha;
    bool hintFull;
    const string PrefHintLines = "BuilderSim.DialogueLines";

    // ------------------------------------------------------------ UI
    const float TailH = 22f;
    const float HintH = 26f;
    RectTransform canvasRt, bubbleRt, tailRt, subRt;
    CanvasGroup bubbleGroup, subGroup;
    Image ring, tailImg;
    TMP_Text nameTxt, bodyTxt, subTxt, hintTxt, noteTxt;
    CanvasGroup hintGroup, holdGroup, noteGroup;
    Image holdRing;
    int blipShown;
    static Sprite tailSprite, ringSprite;

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
        BuildUi();
    }

    void OnDestroy() { if (inst == this) inst = null; }

    // ------------------------------------------------------------ сборка интерфейса
    RectTransform Child(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r, float l, float b, float rt, float tp)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -tp);
    }

    TMP_Text MakeText(RectTransform r, float size, Color c, TMP_FontAsset font, bool outline, TextAlignmentOptions al)
    {
        var tx = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tx.font = font;
        tx.fontSize = size;
        tx.color = c;
        tx.fontStyle = FontStyles.Bold;
        tx.alignment = al;
        tx.raycastTarget = false;
        if (outline)
        {
            tx.outlineWidth = 0.2f;
            tx.outlineColor = new Color32(0x0B, 0x10, 0x20, 255);
        }
        return tx;
    }

    static Sprite TailSprite()
    {
        if (tailSprite != null) return tailSprite;
        const int W = 64, H = 48;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float hw = 28f * (y - 2f) / 44f;               // треугольник остриём вниз
                float d = Mathf.Abs(x + 0.5f - 32f) - hw;
                float a = Mathf.Clamp01(0.5f - d * 0.8f) * (y >= 2 ? 1f : 0f);
                px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        tailSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
        return tailSprite;
    }

    static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        float ro = n * 0.5f - 1f, ri = ro - 7f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ro - d + 0.5f) * Mathf.Clamp01(d - ri + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    void BuildUi()
    {
        TMP_FontAsset font = null;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null)
            font = UIManager.Instance.objectiveText.font;   // шрифт HUD (с кириллицей)

        var cgo = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        var dark = new Color(0.06f, 0.09f, 0.17f, 0.95f);
        var c = new Vector2(0.5f, 0.5f);

        // ---------- пузырь
        bubbleRt = Child("Bubble", canvasRt);
        bubbleRt.anchorMin = bubbleRt.anchorMax = c;
        bubbleRt.pivot = new Vector2(0.5f, 0f);
        bubbleRt.sizeDelta = new Vector2(bubbleWidth, 120f);
        bubbleGroup = bubbleRt.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;
        bubbleGroup.blocksRaycasts = false;
        bubbleGroup.interactable = false;

        var back = Child("Back", bubbleRt);
        Stretch(back, 0, TailH, 0, 0);
        var bi = back.gameObject.AddComponent<Image>();
        bi.sprite = UiKit.Panel; bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 1f / 0.6f; bi.color = Color.white; bi.raycastTarget = false;

        var rr = Child("Ring", back);
        Stretch(rr, 6, 6, 6, 6);                       // цвет говорящего: тонкая линия внутри латунного ободка
        ring = rr.gameObject.AddComponent<Image>();
        ring.sprite = UiDraw.PillRing; ring.type = Image.Type.Sliced; ring.raycastTarget = false;

        var nr = Child("Name", back);
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.anchoredPosition = new Vector2(0f, -14f);
        nr.sizeDelta = new Vector2(-56f, 30f);
        nameTxt = MakeText(nr, 24f, Color.white, font, true, TextAlignmentOptions.MidlineLeft);

        var br = Child("Text", back);
        Stretch(br, 28f, 20f + HintH, 28f, 46f);
        bodyTxt = MakeText(br, 30f, Color.white, font, false, TextAlignmentOptions.TopLeft);
        bodyTxt.fontStyle = FontStyles.Normal;
        bodyTxt.enableWordWrapping = true;

        // строка подсказки внизу пузыря + кольцо удержания справа
        var hr = Child("Hint", back);
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.anchoredPosition = new Vector2(0f, 14f);
        hr.sizeDelta = new Vector2(-84f, HintH);
        hintTxt = MakeText(hr, 20f, new Color(0.68f, 0.73f, 0.84f, 1f), font, false, TextAlignmentOptions.MidlineLeft);
        hintTxt.fontStyle = FontStyles.Normal;
        hintTxt.richText = true;
        hintTxt.enableWordWrapping = false;
        hintTxt.overflowMode = TextOverflowModes.Ellipsis;
        hintGroup = hr.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;

        var gr = Child("HoldRing", back);
        gr.anchorMin = gr.anchorMax = new Vector2(1f, 0f);
        gr.pivot = new Vector2(1f, 0f);
        gr.anchoredPosition = new Vector2(-24f, 12f);
        gr.sizeDelta = new Vector2(34f, 34f);
        var gbg = gr.gameObject.AddComponent<Image>();
        gbg.sprite = RingSprite(); gbg.color = new Color(1f, 1f, 1f, 0.16f); gbg.raycastTarget = false;
        var gf = Child("Fill", gr);
        Stretch(gf, 0, 0, 0, 0);
        holdRing = gf.gameObject.AddComponent<Image>();
        holdRing.sprite = RingSprite(); holdRing.color = new Color32(0x6B, 0xCB, 0x5A, 255); holdRing.raycastTarget = false;
        holdRing.type = Image.Type.Filled; holdRing.fillMethod = Image.FillMethod.Radial360;
        holdRing.fillOrigin = (int)Image.Origin360.Top; holdRing.fillClockwise = true; holdRing.fillAmount = 0f;
        holdGroup = gr.gameObject.AddComponent<CanvasGroup>();
        holdGroup.alpha = 0f;

        tailRt = Child("Tail", bubbleRt);
        tailRt.anchorMin = tailRt.anchorMax = new Vector2(0.5f, 0f);
        tailRt.pivot = new Vector2(0.5f, 0f);
        tailRt.sizeDelta = new Vector2(34f, TailH + 2f);
        tailImg = tailRt.gameObject.AddComponent<Image>();
        tailImg.sprite = TailSprite(); tailImg.color = new Color32(0x1B, 0x2A, 0x3E, 255); tailImg.raycastTarget = false;

        // ---------- субтитры
        subRt = Child("Subtitles", canvasRt);
        subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 0f);
        subRt.pivot = new Vector2(0.5f, 0f);
        subRt.anchoredPosition = new Vector2(0f, 28f);
        subRt.sizeDelta = new Vector2(900f, 70f);
        subGroup = subRt.gameObject.AddComponent<CanvasGroup>();
        subGroup.alpha = 0f;
        subGroup.blocksRaycasts = false;
        subGroup.interactable = false;

        var sb = Child("Back", subRt);
        Stretch(sb, 0, 0, 0, 0);
        var si = sb.gameObject.AddComponent<Image>();
        si.sprite = UiKit.Panel; si.type = Image.Type.Sliced; si.pixelsPerUnitMultiplier = 1f / 0.5f; si.color = new Color(1f, 1f, 1f, 0.94f); si.raycastTarget = false;

        var st = Child("Text", subRt);
        Stretch(st, 36f, 12f, 36f, 12f);
        subTxt = MakeText(st, 32f, Color.white, font, true, TextAlignmentOptions.Center);
        subTxt.fontStyle = FontStyles.Normal;
        subTxt.richText = true;
        subTxt.enableWordWrapping = true;

        // плашка «Разговор пропущен»: чуть выше субтитров, живёт ~1.3 с
        var nt = Child("SkipNote", canvasRt);
        nt.anchorMin = nt.anchorMax = new Vector2(0.5f, 0f);
        nt.pivot = new Vector2(0.5f, 0f);
        nt.anchoredPosition = new Vector2(0f, 120f);
        nt.sizeDelta = new Vector2(430f, 52f);
        noteGroup = nt.gameObject.AddComponent<CanvasGroup>();
        noteGroup.alpha = 0f; noteGroup.blocksRaycasts = false; noteGroup.interactable = false;
        var nb = Child("Back", nt);
        Stretch(nb, 0, 0, 0, 0);
        var ni = nb.gameObject.AddComponent<Image>();
        ni.sprite = UiKit.Panel; ni.type = Image.Type.Sliced; ni.pixelsPerUnitMultiplier = 1f / 0.5f; ni.color = new Color(1f, 1f, 1f, 0.92f); ni.raycastTarget = false;
        var nx = Child("Text", nt);
        Stretch(nx, 16f, 6f, 16f, 6f);
        noteTxt = MakeText(nx, 26f, new Color(0.9f, 0.93f, 1f, 1f), font, true, TextAlignmentOptions.Center);
        noteTxt.fontStyle = FontStyles.Normal;
        noteTxt.text = "Разговор пропущен";
    }

    // ------------------------------------------------------------ очередь
    void Enqueue(Speaker who, string text, AudioClip clip, Action onDone)
    {
        if (string.IsNullOrEmpty(text)) { if (onDone != null) onDone(); return; }
        // нет своего клипа: ищем файл озвучки по id реплики (Resources/Voice), иначе будет бормотание
        if (clip == null) clip = VoiceBank.Find(who != null ? who.displayName : "", text);
        // голоса выключены ползунком: реплика идёт по тексту, без ожидания длины невидимой озвучки
        if (GameAudio.VoiceLevel <= 0.001f) clip = null;
        if (text.Length > maxChars)
        {
            Debug.LogWarning("Dialogue: реплика длиннее " + maxChars + " символов, обрезана: " + text);
            text = text.Substring(0, maxChars - 1) + "…";
        }

        float dur = clip != null
            ? Mathf.Max(4f, clip.length + 0.5f)
            : Mathf.Clamp(3.2f + text.Length * 0.02f, 4f, 5f);   // 4-5 секунд

        queue.Enqueue(new Req { who = who, text = text, clip = clip, onDone = onDone, dur = dur });
    }

    // запасной голос для реплик без Speaker (раньше такая озвучка молча пропадала)
    AudioSource orphanVoice;

    void PlayOrphanVoice(AudioClip c)
    {
        if (c == null) return;
        if (orphanVoice == null)
        {
            orphanVoice = gameObject.AddComponent<AudioSource>();
            orphanVoice.playOnAwake = false;
            orphanVoice.spatialBlend = 0f;
        }
        orphanVoice.Stop();
        orphanVoice.clip = c;
        orphanVoice.volume = GameAudio.VoiceLevel;
        orphanVoice.Play();
    }

    void Begin(Req r)
    {
        cur = r;
        t = 0f; typed = 0f; typedDone = false; shown = 0f; blipShown = 0;
        pressing = false; holdProgress = 0f; hintAlpha = 0f; outTime = 0.3f;
        DialogueLog.Add(r.who != null ? r.who.displayName : "", r.text, false);

        // подсказка: первые реплики в игре с полным текстом, потом короткая «E: дальше»
        int seen = PlayerPrefs.GetInt(PrefHintLines, 0);
        hintFull = seen < fullHintLines;
        if (hintFull) PlayerPrefs.SetInt(PrefHintLines, seen + 1);
        hintTxt.text = HintText(hintFull);

        string who = r.who != null ? r.who.displayName : "";
        Color col = r.who != null ? r.who.nameColor : Color.white;

        // ----- пузырь
        nameTxt.text = who;
        nameTxt.color = col;
        bodyTxt.text = r.text;
        total = r.text.Length;
        bodyTxt.maxVisibleCharacters = 0;

        float w = bubbleWidth;
        Vector2 pref = bodyTxt.GetPreferredValues(r.text, w - 56f, 0f);
        float h = 14f + 30f + pref.y + 26f + HintH;
        bubbleRt.sizeDelta = new Vector2(w, h + TailH);
        ring.color = new Color(col.r, col.g, col.b, 0.95f);

        // ----- субтитры
        string hex = ColorUtility.ToHtmlStringRGB(col);
        string line = who.Length > 0 ? "<color=#" + hex + "><b>" + who + ":</b></color> " + r.text : r.text;
        subTxt.text = line;
        Vector2 sp = subTxt.GetPreferredValues(line, 1000f, 0f);
        subRt.sizeDelta = new Vector2(Mathf.Min(1080f, sp.x + 76f), sp.y + 28f);

        Speaker.StopAllVoices(r.who);                 // хвост предыдущей реплики не должен накладываться на новую
        if (orphanVoice != null && r.who != null) orphanVoice.Stop();
        if (r.who != null) r.who.PlayVoice(r.clip);
        else PlayOrphanVoice(r.clip);                 // говорящего не нашли (SayAs с неизвестным именем): озвучка всё равно звучит
        PlaceBubble();
    }

    void Finish(bool skipped)
    {
        var r = cur;
        cur = null;
        gap = 0.2f;
        if (r == null) return;
        if (skipped && r.who != null) r.who.FadeOutVoice(0.14f);
        else if (skipped && orphanVoice != null) orphanVoice.Stop();
        if (r.onDone != null) r.onDone();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (cur == null)
        {
            gap -= dt;
            if (gap <= 0f && queue.Count > 0) Begin(queue.Dequeue());
        }
        else
        {
            t += dt;

            if (!typedDone)
            {
                typed += typeSpeed * dt;
                int vis = Mathf.Min(total, (int)typed);
                // бормотание вместо озвучки (GDD 12.3): звук на новые буквы, если у реплики нет своего клипа
                while (blipShown < vis)
                {
                    if (cur.who != null && cur.clip == null) SfxManager.Blip(cur.who.displayName, cur.text, blipShown);
                    blipShown++;
                }
                bodyTxt.maxVisibleCharacters = vis;
                if (vis >= total) typedDone = true;
            }

            HandleInput(dt);
            if (cur != null && t >= cur.dur) Finish(false);
        }

        if (BlocksUntilRelease && !MobileInput.ActionHeld) BlocksUntilRelease = false;

        // плавное появление и исчезновение
        bool want = cur != null && t < cur.dur - 0.3f;
        shown = Mathf.MoveTowards(shown, want ? 1f : 0f, dt / (want ? 0.18f : outTime));

        bool hasBubble = cur != null && cur.who != null;
        bubbleGroup.alpha = hasBubble || shown > 0f ? Mathf.Clamp01(shown * 1.6f) : 0f;
        subGroup.alpha = SubtitlesEnabled ? Mathf.Clamp01(shown * 1.6f) : 0f;
        bubbleRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, UiDraw.EaseOutBack(Mathf.Clamp01(shown))) * (1f - 0.035f * holdProgress);

        // подсказка проявляется, когда реплику уже можно пропустить; кольцо удержания следует за прогрессом
        float wantHint = cur != null && showSkipHint && t > minShowBeforeSkip ? 1f : 0f;
        hintAlpha = Mathf.MoveTowards(hintAlpha, wantHint, dt / 0.25f);
        hintGroup.alpha = hintAlpha;
        holdRing.fillAmount = holdProgress;
        holdGroup.alpha = holdProgress > 0f ? 1f : (hintAlpha * 0.55f);   // слабый серый круг рядом с подсказкой, пока не держат

        // плашка «Разговор пропущен»: быстро появляется и мягко гаснет
        if (noteT > 0f)
        {
            noteT -= dt;
            noteGroup.alpha = Mathf.Clamp01(noteT / 0.35f) * Mathf.Clamp01((1.3f - noteT) / 0.12f);
        }
        else noteGroup.alpha = 0f;

        if (cur != null) PlaceBubble();
    }

    // тап = «дальше», удержание = пропустить разговор. «Дальше» срабатывает при отпускании, иначе тап нельзя отличить от удержания
    void HandleInput(float dt)
    {
        bool canSkip = t > minShowBeforeSkip;
        if (canSkip && !pressing && MobileInput.ActionDown) { pressing = true; pressT = 0f; }
        if (!pressing) { holdProgress = 0f; return; }

        if (MobileInput.ActionHeld)
        {
            pressT += dt;
            holdProgress = Mathf.Clamp01((pressT - holdShowDelay) / Mathf.Max(0.05f, skipAllHoldTime - holdShowDelay));
            if (pressT >= skipAllHoldTime)
            {
                pressing = false;
                holdProgress = 0f;
                DoSkipAll();
            }
            return;
        }

        // отпустил раньше: обычное «дальше»
        pressing = false;
        holdProgress = 0f;
        if (!typedDone)
        {
            bodyTxt.maxVisibleCharacters = total;
            typed = total;
            typedDone = true;      // первое нажатие: дописать, второе: убрать
        }
        else Finish(true);
    }

    string HintText(bool full)
    {
        string key = MobileInput.OnScreenControls ? "кнопка" : MobileInput.ActionKey.ToString();
        if (key.StartsWith("Alpha")) key = key.Substring(5);
        string k = "<color=#6BCB5A><b>" + key + "</b></color>";
        return full ? k + " дальше   ·   удерживай " + k + ", чтобы пропустить разговор" : k + " дальше   ·   удерживай, чтобы пропустить";
    }

    void DoSkipAll()
    {
        SkipAllRequested = true;
        BlocksUntilRelease = true;
        outTime = 0.12f;
        noteT = 1.3f;

        // очередь снимаем сами: onDone каждой реплики обязан сработать, иначе кто-то будет ждать её вечно
        var pending = queue.ToArray();
        queue.Clear();
        if (cur != null) Finish(true);
        foreach (var r in pending)
        {
            DialogueLog.Add(r.who != null ? r.who.displayName : "", r.text, true);
            if (r.onDone != null) r.onDone();
        }
        gap = 0f;
        // одиночная реплика не из DialogueSequence: флаг никому не нужен
        if (!DialogueSequence.AnyPlaying) SkipAllRequested = false;
    }

    // пузырь живёт в экранных координатах: размер постоянный, за деревьями не прячется, у края экрана не вылезает
    void PlaceBubble()
    {
        if (cur == null || cur.who == null) { bubbleGroup.alpha = 0f; return; }
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 sp = cam.WorldToScreenPoint(cur.who.BubblePoint);
        if (sp.z < 0.1f) { bubbleGroup.alpha = 0f; return; }

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, new Vector2(sp.x, sp.y), null, out local);

        Vector2 half = canvasRt.rect.size * 0.5f;
        float w = bubbleRt.sizeDelta.x, h = bubbleRt.sizeDelta.y;
        float x = Mathf.Clamp(local.x, -half.x + w * 0.5f + 16f, half.x - w * 0.5f - 16f);
        float y = Mathf.Clamp(local.y + 4f, -half.y + 16f, half.y - h - 16f);
        bubbleRt.anchoredPosition = new Vector2(x, y);

        // хвостик показывает на говорящего, даже если пузырь прижало к краю экрана
        float tx = Mathf.Clamp(local.x - x, -w * 0.5f + 48f, w * 0.5f - 48f);
        tailRt.anchoredPosition = new Vector2(tx, 0f);
    }
}