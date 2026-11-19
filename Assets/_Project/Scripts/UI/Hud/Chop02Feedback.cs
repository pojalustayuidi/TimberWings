using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class Chop02Feedback : MonoBehaviour
{
    static Chop02Feedback instance;
    static readonly Color Cream = new Color32(255, 239, 211, 249);
    static readonly Color Ink = new Color32(86, 58, 43, 255);
    static readonly Color Track = new Color32(211, 185, 156, 255);
    static readonly Color Gold = new Color32(224, 165, 74, 255);
    static readonly Color Green = new Color32(149, 185, 130, 255);
    static readonly Color Lavender = new Color32(114, 88, 155, 255);
    const float ResultSeconds = .68f;
    const float LessonSeconds = 1.48f;

    Canvas canvas;
    CanvasGroup group;
    RectTransform card, progressTrack, timingTrack, pointer;
    Image progressFill, zone, ideal, pointerImage;
    TextMeshProUGUI prompt, verdict;
    Vector3 point, resultPoint;
    string label;
    bool wanted, working, firstLesson;
    float progress, reportAt = -10f, reportUntil, cardWidth, cardHeight;
    float lastGoodHalf, lastPerfectHalf;
    bool lastMeter, lastProgress, lastResultRow, lastActionRow;
    Chop03Swing timing;
    ChopTimingResult result;

    internal static ChopTimingResult RecentResult => instance != null &&
        Time.unscaledTime - instance.reportAt < .22f ? instance.result : ChopTimingResult.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() => instance = null;

    static Chop02Feedback Display()
    {
        if (instance != null) return instance;
        var go = new GameObject("Chop feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.layer = 5;
        return instance = go.AddComponent<Chop02Feedback>();
    }

    internal static void Show(Vector3 at, string text, float progress = -1f, Chop03Swing timing = default,
        bool working = false)
    {
        var display = Display();
        display.point = at;
        display.wanted = true;
        display.progress = progress;
        display.timing = timing;
        display.working = working;
        display.label = text;
    }

    internal static void Report(ChopTimingResult result, Vector3 contact, bool lesson = false)
    {
        var display = Display();
        display.result = result;
        display.resultPoint = contact;
        display.firstLesson = lesson;
        display.reportAt = Time.unscaledTime;
        display.reportUntil = display.reportAt + (lesson ? LessonSeconds : ResultSeconds);
    }

    internal static void Hide(bool immediate = false)
    {
        if (instance == null) return;
        instance.wanted = false;
        if (!immediate) return;
        instance.reportAt = -10f;
        instance.reportUntil = 0f;
        instance.group.alpha = 0f;
        instance.canvas.enabled = false;
    }

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.WorldTag + 1;
        canvas.enabled = false;
        HudGameplayLayout.Configure(GetComponent<CanvasScaler>());

        var panel = ImageAt(transform, "Cream card", Cream);
        panel.sprite = UiDraw.Pill;
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 2.3f;
        var shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.24f, .15f, .12f, .46f);
        shadow.effectDistance = new Vector2(0f, -2f);
        card = panel.rectTransform;
        card.anchorMin = card.anchorMax = Vector2.zero;
        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        group.alpha = 0f;

        var hud = UIManager.Instance;
        var font = hud != null && hud.objectiveText != null ? hud.objectiveText.font : null;
        prompt = CandyTag.Text(card, "Action", 20f, Ink, TextAlignmentOptions.Center, true, font);
        verdict = CandyTag.Text(card, "Result", 20f, Ink, TextAlignmentOptions.Center, true, font);
        PrepareLabel(prompt);
        PrepareLabel(verdict);

        progressTrack = PillAt(card, "Notch track", Track).rectTransform;
        progressFill = PillAt(progressTrack, "Notch progress", Gold);
        RtFill(progressFill.rectTransform);

        timingTrack = PillAt(card, "Timing track", Track).rectTransform;
        zone = PillAt(timingTrack, "Green zone", Green);
        ideal = PillAt(timingTrack, "Gold zone", Gold);
        pointerImage = PillAt(timingTrack, "Timing marker", Ink);
        pointer = pointerImage.rectTransform;
        pointer.anchorMin = pointer.anchorMax = new Vector2(0f, .5f);
        pointer.sizeDelta = new Vector2(5f, 19f);
        var markerOutline = pointer.gameObject.AddComponent<Outline>();
        markerOutline.effectColor = new Color(1f, .97f, .88f, .86f);
        markerOutline.effectDistance = new Vector2(1f, -1f);
    }

    static void PrepareLabel(TextMeshProUGUI label)
    {
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 15f;
        label.fontSizeMax = 20f;
    }

    void LateUpdate()
    {
        if (HudGameplayLayout.Blocked)
        {
            Hide(true);
            return;
        }
        float now = Time.unscaledTime;
        bool withResult = now < reportUntil;
        if (!wanted && !withResult && !canvas.enabled) return;
        Camera cam = Camera.main;
        Vector3 screen = cam != null ? cam.WorldToScreenPoint(wanted ? point : resultPoint) : Vector3.zero;
        Rect safe = HudGameplayLayout.SafeArea;
        bool inView = screen.z > .08f && safe.Contains(screen);
        bool fixedCard = OrbitCamera.FirstPersonActive && (!wanted || working || inView);
        bool show = (wanted || withResult) && (inView || fixedCard);
        bool reduce = VibeSettings.ReduceEffects;
        group.alpha = reduce ? (show ? 1f : 0f) :
            Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 15f : 10f));
        canvas.enabled = group.alpha > .005f;
        if (!show) return;

        bool withMeter = wanted && timing.Armed;
        bool withProgress = wanted && progress >= 0f;
        string action = wanted ? label : null;
        if (withResult && firstLesson && now - reportAt > .38f)
            action = ChopPrototypeLog.Text("Золотая отметка усиливает удар", "The gold mark strengthens the hit");
        SetLabel(prompt, action);
        SetLabel(verdict, withResult ? ResultText(result) : null);
        float age = now - reportAt;
        verdict.color = ResultColor(result);
        verdict.rectTransform.localScale = Vector3.one * (reduce || !withResult
            ? 1f : 1f + .065f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(age / .22f)));

        Layout(action != null, withResult, withProgress, withMeter);
        float scale = Mathf.Max(.01f, canvas.scaleFactor);
        float fit = Mathf.Min(HudGameplayLayout.Fit(canvas, cardWidth, .92f),
            (safe.height - 18f) / Mathf.Max(1f, scale * cardHeight));
        fit = Mathf.Max(.1f, fit);
        card.localScale = Vector3.one * fit;
        scale *= fit;
        float halfWidth = cardWidth * scale * .5f;
        float halfHeight = cardHeight * scale * .5f;
        Vector2 viewportCenter = cam != null ? cam.pixelRect.center : safe.center;
        Vector2 anchor = fixedCard
            ? viewportCenter - new Vector2(0f, (98f + cardHeight * .35f) * scale)
            : new Vector2(screen.x, screen.y + (33f + cardHeight * .5f) * scale);
        card.position = new Vector2(
            Mathf.Clamp(anchor.x, safe.xMin + halfWidth + 8f, safe.xMax - halfWidth - 8f),
            Mathf.Clamp(anchor.y, safe.yMin + halfHeight + 8f, safe.yMax - halfHeight - 8f));
        if (withProgress)
        {
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            progressFill.color = Color.Lerp(Gold, Green,
                !reduce && result == ChopTimingResult.Perfect ? Mathf.Clamp01(1f - age / .22f) : 0f);
        }
        if (!withMeter) return;
        float marker = timing.Marker(Time.time);
        pointer.anchoredPosition = new Vector2(marker * timingTrack.rect.width, 0f);
        bool inside = timing.InGoodZone(marker) && !timing.Attempted;
        zone.color = inside ? new Color32(129, 179, 116, 255) : Green;
        pointerImage.color = timing.Attempted ? Lavender : Ink;
    }

    void Layout(bool withText, bool withResult, bool withProgress, bool withMeter)
    {
        const float textRow = 25f;
        bool actionRow = withText || withMeter;
        float baseWidth = withMeter ? 268f : 192f;
        float width = withText ? Mathf.Clamp(prompt.preferredWidth + 30f, baseWidth, 338f) : baseWidth;
        float height = 16f + (actionRow ? textRow : 0f) + (withResult ? textRow : 0f) +
            (withProgress ? 9f : 0f) + (withMeter ? 21f : 0f);
        if (width == cardWidth && height == cardHeight && lastMeter == withMeter && lastProgress == withProgress &&
            lastResultRow == withResult && lastActionRow == actionRow &&
            (!withMeter || lastGoodHalf == timing.GoodHalf && lastPerfectHalf == timing.PerfectHalf)) return;
        cardWidth = width;
        cardHeight = height;
        lastMeter = withMeter;
        lastProgress = withProgress;
        lastResultRow = withResult;
        lastActionRow = actionRow;
        lastGoodHalf = timing.GoodHalf;
        lastPerfectHalf = timing.PerfectHalf;
        card.sizeDelta = new Vector2(width, height);
        float y = height * .5f - 8f;

        if (withResult)
        {
            Place(verdict.rectTransform, width - 16f, textRow, y - textRow * .5f);
            y -= textRow;
        }
        if (actionRow)
        {
            Place(prompt.rectTransform, width - 20f, textRow, y - textRow * .5f);
            y -= textRow;
        }
        timingTrack.gameObject.SetActive(withMeter);
        if (withMeter)
        {
            float span = width - 34f;
            Place(timingTrack, span, 10f, y - 10f);
            Place(zone.rectTransform, span * 2f * timing.GoodHalf, 10f, 0f);
            Place(ideal.rectTransform, span * 2f * timing.PerfectHalf, 10f, 0f);
            y -= 21f;
        }
        progressTrack.gameObject.SetActive(withProgress);
        if (withProgress) Place(progressTrack, width - 34f, 4f, y - 4f);
    }

    internal static string ResultText(ChopTimingResult result) => result switch
    {
        ChopTimingResult.Perfect => ChopPrototypeLog.Text("Идеально!", "Perfect!"),
        ChopTimingResult.Good => ChopPrototypeLog.Text("Хорошо!", "Good!"),
        ChopTimingResult.Early => ChopPrototypeLog.Text("Рано", "Early"),
        ChopTimingResult.Late => ChopPrototypeLog.Text("Поздно", "Late"),
        ChopTimingResult.Miss => ChopPrototypeLog.Text("Промах", "Miss"),
        _ => string.Empty
    };

    static Color ResultColor(ChopTimingResult result) => result switch
    {
        ChopTimingResult.Perfect => new Color32(129, 76, 21, 255),
        ChopTimingResult.Good => new Color32(45, 102, 65, 255),
        _ => Lavender
    };

    static void SetLabel(TextMeshProUGUI text, string label)
    {
        bool show = !string.IsNullOrEmpty(label);
        if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
        if (show && text.text != label) text.text = label;
    }

    static Image ImageAt(Transform parent, string name, Color tint)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    static Image PillAt(Transform parent, string name, Color tint)
    {
        var image = ImageAt(parent, name, tint);
        image.sprite = UiDraw.Pill;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 3.4f;
        return image;
    }

    static void RtFill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rect, float width, float height, float y)
    {
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, y);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
