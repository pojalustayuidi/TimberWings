// Assets/_Project/Scripts/Dialogue/DialogueManager.cs (НЕ в Editor)
// Показывает реплики: пузырь над говорящим (до 120 символов, печатается по буквам,
// исчезает через 4-5 с или по E) и субтитры внизу экрана (по умолчанию включены).
// Создаётся сам при первой реплике, на сцену вешать ничего не нужно.
// Вид (Candy: облачный пузырь, лента с именем, субтитры, мягкая печать) лежит в DialogueManager.Candy.cs, здесь логика.
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

public partial class DialogueManager : MonoBehaviour
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

    // ------------------------------------------------------------ UI (поля и сборка: DialogueManager.Candy.cs)
    int blipShown;
    CandySpring pop;   // пружина появления пузыря

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
        bodyTxt.text = r.text;
        total = r.text.Length;
        bodyTxt.maxVisibleCharacters = 0;
        softDirty = false;
        float topPad = ApplySpeaker(who, col);   // лента с именем, отступы текста

        float w = bubbleWidth;
        Vector2 pref = bodyTxt.GetPreferredValues(r.text, w - 56f, 0f);
        float h = topPad + pref.y + BottomPad + HintH + 4f;
        bubbleRt.sizeDelta = new Vector2(w, h + TailH);
        pop = new CandySpring(0.72f);            // пузырь выскакивает пружиной

        // ----- субтитры
        string hex = ColorUtility.ToHtmlStringRGB(NameInk(col));
        string line = who.Length > 0 ? "<color=#" + hex + "><b>" + who + ":</b></color> " + r.text : r.text;
        subTxt.text = line;
        Vector2 sp = subTxt.GetPreferredValues(line, 1000f, 0f);
        subRt.sizeDelta = new Vector2(Mathf.Min(1080f, sp.x + 76f), sp.y + SubPadTop + SubPadBottom);

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

            // typed растёт ещё SoftChars букв после конца текста: так последние буквы успевают мягко проявиться
            if (typed < total + SoftChars)
            {
                typed += typeSpeed * dt;
                if (!typedDone)
                {
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
            }
            SoftType();

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
        pop.Step(want ? 1f : 0.9f, 260f, 15f, Mathf.Min(dt, 0.05f));
        bubbleRt.localScale = Vector3.one * pop.x * (1f - 0.035f * holdProgress);

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
            typed = total + SoftChars;   // без мягкого проявления: сразу весь текст
            typedDone = true;      // первое нажатие: дописать, второе: убрать
        }
        else Finish(true);
    }

    string HintText(bool full)
    {
        string key = MobileInput.OnScreenControls ? "кнопка" : MobileInput.ActionKey.ToString();
        if (key.StartsWith("Alpha")) key = key.Substring(5);
        string k = "<color=#2A9573><b>" + key + "</b></color>";
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
        float y = Mathf.Clamp(local.y + 4f, -half.y + 16f, half.y - h - 16f - PillOver);
        bubbleRt.anchoredPosition = new Vector2(x, y);

        // хвостик показывает на говорящего, даже если пузырь прижало к краю экрана
        float tx = Mathf.Clamp(local.x - x, -w * 0.5f + 48f, w * 0.5f - 48f);
        tailRt.anchoredPosition = new Vector2(tx, 0f);
    }
}