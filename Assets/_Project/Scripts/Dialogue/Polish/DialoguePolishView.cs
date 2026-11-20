using TMPro;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// The editor builder authors the objects. Runtime only updates existing references.
[DisallowMultipleComponent]
public sealed class DialoguePolishView : MonoBehaviour
{
    public Canvas canvas;
    public RectTransform panel;
    public CanvasGroup group, hintGroup, holdGroup, noteGroup;
    public TMP_Text speakerName, role, topic, body, progress, hint, nextLabel, monogram, noteText;
    public Image avatarBack, emblem, holdRing;
    public Button nextButton, skipButton;
    public Button historyButton, autoButton, historyClose;
    public RectTransform historyRoot;
    public TMP_Text historyText, autoLabel;
    public ScrollRect historyScroll;
    public bool HistoryOpen { get { return historyRoot != null && historyRoot.gameObject.activeSelf; } }
    public RectTransform[] voiceBars;
    public DialogueAudioLibrary library;
    Vector2 lastSize;
    Rect lastSafe;
    bool registered, lastTyped, lastHistory;
    string lastText, lastSpeaker;
    int lastLine = -1, lastCount = -1;

    void Start()
    {
        nextButton.onClick.AddListener(DialogueManager.Advance);
        skipButton.onClick.AddListener(DialogueManager.SkipFromUI);
        if (historyButton != null) historyButton.onClick.AddListener(ToggleHistory);
        if (historyClose != null) historyClose.onClick.AddListener(CloseHistory);
        if (autoButton != null) autoButton.onClick.AddListener(ToggleAuto);
        CloseHistory();
        group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
        Layout();
    }
    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(DialogueManager.Advance);
        if (skipButton != null) skipButton.onClick.RemoveListener(DialogueManager.SkipFromUI);
        if (historyButton != null) historyButton.onClick.RemoveListener(ToggleHistory);
        if (historyClose != null) historyClose.onClick.RemoveListener(CloseHistory);
        if (autoButton != null) autoButton.onClick.RemoveListener(ToggleAuto);
    }
    void OnDisable()
    {
        if (registered && panel != null && HudSafeAreaRegistry.Instance != null) HudSafeAreaRegistry.Instance.Unregister(panel);
        registered = false;
    }
    public void SetLine(string who, string text, Color fallback)
    {
        CloseHistory();
        lastSpeaker = string.IsNullOrEmpty(who) ? "Лес" : who;
        lastText = text;
        var book = library != null ? library : DialogueAudioLibrary.Current;
        var actor = book != null ? book.Actor(who) : null;
        Color color = actor != null ? actor.tint : Color.Lerp(fallback, Color.white, .6f);
        speakerName.text = ChapterOneText.SpeakerName(lastSpeaker);
        speakerName.color = CandyTag.Ink;
        role.text = GameLanguage.T(actor != null ? actor.role : who == RicoMerchant.DisplayName ? "Торговец у реки" : "Голос рядом");
        avatarBack.color = color;
        monogram.text = speakerName.text.Substring(0, 1).ToUpperInvariant();
        emblem.sprite = actor != null ? actor.emblem : null;
        emblem.enabled = emblem.sprite != null;
        topic.text = GameLanguage.T(string.IsNullOrEmpty(DialogueSequence.ActiveTitle) ? "Разговор в лагере" : DialogueSequence.ActiveTitle).ToUpperInvariant();
        Layout();
    }
    public void Layout()
    {
        if (canvas == null || panel == null || body == null) return;
        lastSize = ((RectTransform)canvas.transform).rect.size; lastSafe = Screen.safeArea;
        float scale = Mathf.Max(.01f, canvas.scaleFactor);
        float available = Mathf.Min(lastSize.x, lastSafe.width / scale) - 48f;
        float width = Mathf.Max(320f, Mathf.Min(1080f, available));
        body.fontSize = width < 800f ? 27f : 30f;
        float preferred = body.GetPreferredValues(lastText ?? "", width - 180f, 0).y;
        float height = Mathf.Clamp(178f + preferred, 240f, 380f);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0f);
        float center = (lastSafe.center.x - Screen.width * .5f) / scale;
        panel.anchoredPosition = new Vector2(center, lastSafe.yMin / scale + 46f);
        panel.sizeDelta = new Vector2(width, height);
        topic.gameObject.SetActive(width >= 940f);
        if (GameLanguage.English)
            hint.text = (MobileInput.OnScreenControls ? "Button" : MobileInput.ActionKey.ToString()) + (width < 940f ? " to continue" : " to continue · hold to skip");
        else hint.text = width < 940f ? (MobileInput.OnScreenControls ? "Кнопка: дальше" : MobileInput.ActionKey + ": дальше") : MobileInput.OnScreenControls ? "Кнопка: дальше · удержание: пропуск" : MobileInput.ActionKey + ": дальше · удержание: пропуск";
    }
    public void Tick(bool hasLine, bool typedDone, bool canAdvance, bool talking)
    {
        if (!hasLine) CloseHistory();
        if (autoLabel != null) autoLabel.text = GameLanguage.T(DialoguePreferences.AutoAdvance ? "Авто: вкл" : "Авто: выкл");
        if (historyButton != null) historyButton.interactable = hasLine;
        if (canvas != null && (lastSize != ((RectTransform)canvas.transform).rect.size || lastSafe != Screen.safeArea)) Layout();
        group.blocksRaycasts = hasLine; group.interactable = hasLine;
        nextButton.interactable = hasLine && canAdvance;
        skipButton.interactable = hasLine && canAdvance;
        int line = DialogueSequence.ActiveLine, count = DialogueSequence.ActiveLineCount;
        if (line != lastLine || count != lastCount || lastTyped != typedDone || lastHistory != HistoryOpen)
        {
            lastHistory = HistoryOpen;
            lastLine = line; lastCount = count; lastTyped = typedDone;
            progress.text = count > 1 ? Mathf.Clamp(line, 1, count) + " / " + count : "";
            nextLabel.text = GameLanguage.T(HistoryOpen ? "К разговору" : !typedDone ? "Показать текст" : count > 0 && line >= count ? "Готово" : "Дальше");
            skipButton.gameObject.SetActive(count > 1);
        }
        if (!registered && HudSafeAreaRegistry.Instance != null)
        { HudSafeAreaRegistry.Instance.Register(panel); registered = true; }
        if (voiceBars == null) return;
        for (int i = 0; i < voiceBars.Length; i++) if (voiceBars[i] != null)
        {
            float h = talking && !VibeSettings.ReduceEffects ? 5f + 10f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f + i * 1.3f)) : 4f;
            var size = voiceBars[i].sizeDelta; size.y = h; voiceBars[i].sizeDelta = size;
        }
    }
    void ToggleAuto() { DialoguePreferences.AutoAdvance = !DialoguePreferences.AutoAdvance; }
    public void CloseHistory() { if (historyRoot != null) historyRoot.gameObject.SetActive(false); }
    void ToggleHistory()
    {
        if (historyRoot == null || historyText == null) return;
        if (HistoryOpen) { CloseHistory(); return; }
        var text = new StringBuilder();
        foreach (var entry in DialogueLog.Entries)
        {
            if (text.Length > 0) text.Append("\n\n");
            if (!string.IsNullOrEmpty(entry.speaker)) text.Append("<b>").Append(ChapterOneText.SpeakerName(entry.speaker)).Append(":</b> ");
            text.Append(ChapterOneText.Subtitle(entry.speaker, entry.text));
            if (entry.skipped) text.Append(ChapterOneText.Pick(" <color=#6B728F>(пропущено)</color>", " <color=#6B728F>(skipped)</color>"));
        }
        historyText.text = text.Length > 0 ? text.ToString()  : ChapterOneText.Pick("Здесь появятся реплики разговора.", "Conversation lines will appear here.");
        historyRoot.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (historyScroll != null) historyScroll.verticalNormalizedPosition = 0;
    }
}
