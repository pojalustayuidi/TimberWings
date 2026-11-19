using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class Chop02Feedback : MonoBehaviour
{
    static Chop02Feedback instance;
    static readonly Color Wood = new Color32(0x3E, 0x2C, 0x1F, 247);
    static readonly Color Track = new Color32(0x72, 0x58, 0x40, 255);
    static readonly Color Gold = new Color32(0xE4, 0xB2, 0x66, 255);
    static readonly Color Green = new Color32(0x9E, 0xB9, 0x65, 255);

    Canvas canvas;
    CanvasGroup group;
    RectTransform card, progressTrack, timingTrack, pointer;
    Image progressFill, zone, pointerImage;
    TextMeshProUGUI prompt;
    Vector3 point;
    string label;
    bool wanted, meter, attempted, precise, working;
    float progress, marker, pulseUntil;
    float cardWidth, cardHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() => instance = null;

    internal static void Show(Vector3 at, string text, float progress = -1f, bool meter = false,
        float marker = 0f, bool attempted = false, bool precise = false, bool working = false)
    {
        if (instance == null)
        {
            var go = new GameObject("Chop feedback", typeof(RectTransform), typeof(Canvas));
            instance = go.AddComponent<Chop02Feedback>();
        }
        var display = instance;
        display.point = at;
        display.wanted = true;
        display.progress = progress;
        display.meter = meter;
        display.marker = marker;
        display.attempted = attempted;
        display.precise = precise;
        display.working = working;
        display.ApplyLabel(text);
    }

    internal static void Hide(bool immediate = false)
    {
        if (instance == null) return;
        instance.wanted = false;
        if (immediate)
        {
            instance.pulseUntil = 0f;
            instance.group.alpha = 0f;
            instance.canvas.enabled = false;
        }
    }

    internal static void Pulse()
    {
        if (instance != null && !VibeSettings.ReduceEffects)
            instance.pulseUntil = Time.unscaledTime + .19f;
    }

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.WorldTag + 1;
        canvas.enabled = false;
        var panel = ImageAt(transform, "Timber panel", Wood);
        panel.sprite = UiKit.PanelWood;
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 2.8f;
        card = panel.rectTransform;
        card.anchorMin = card.anchorMax = Vector2.zero;
        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        group.alpha = 0f;

        var hud = UIManager.Instance;
        var font = hud != null && hud.objectiveText != null ? hud.objectiveText.font : null;
        prompt = CandyTag.Text(card, "Action", 15f, new Color32(0xF8, 0xE4, 0xBE, 255),
            TextAlignmentOptions.Center, true, font);
        prompt.textWrappingMode = TextWrappingModes.NoWrap;

        progressTrack = ImageAt(card, "Notch track", Track).rectTransform;
        progressFill = ImageAt(progressTrack, "Notch progress", Gold);
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = Vector2.one;
        progressFill.rectTransform.offsetMin = progressFill.rectTransform.offsetMax = Vector2.zero;

        timingTrack = ImageAt(card, "Timing track", Track).rectTransform;
        zone = ImageAt(timingTrack, "Green zone", Green);
        pointerImage = ImageAt(timingTrack, "Timing marker", Gold);
        pointer = pointerImage.rectTransform;
        pointer.anchorMin = pointer.anchorMax = new Vector2(0f, .5f);
        pointer.sizeDelta = new Vector2(6f, 6f);
        pointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void ApplyLabel(string text)
    {
        if (label == text && prompt.gameObject.activeSelf == !string.IsNullOrEmpty(text)) return;
        label = text;
        prompt.text = text ?? string.Empty;
        prompt.gameObject.SetActive(!string.IsNullOrEmpty(label));
    }

    void LateUpdate()
    {
        if (!wanted && !canvas.enabled) return;
        Camera cam = Camera.main;
        Vector3 screen = cam != null ? cam.WorldToScreenPoint(point) : Vector3.zero;
        Rect safe = Screen.safeArea;
        bool inView = screen.z > .08f && screen.x >= safe.xMin && screen.x <= safe.xMax &&
            screen.y >= safe.yMin && screen.y <= safe.yMax;
        bool fixedCard = working && OrbitCamera.FirstPersonActive;
        bool show = wanted && (inView || fixedCard);
        float alpha = VibeSettings.ReduceEffects
            ? (show ? 1f : 0f)
            : Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 10f : 8f));
        group.alpha = alpha;
        canvas.enabled = alpha > .005f;
        if (!canvas.enabled || !inView && !fixedCard) return;

        ApplyLayout();
        float scale = Mathf.Clamp(Mathf.Min(safe.width / 1280f, safe.height / 720f), .78f, 1.48f);
        float halfWidth = cardWidth * scale * .5f;
        float halfHeight = cardHeight * scale * .5f;
        float side = screen.x > safe.center.x + safe.width * .19f ? -1f : 1f;
        card.localScale = Vector3.one * scale;
        Vector2 anchor = fixedCard
            ? safe.center - new Vector2(0f, 105f * scale)
            : new Vector2(screen.x + side * 34f * scale, screen.y + (31f + cardHeight * .5f) * scale);
        card.anchoredPosition = new Vector2(
            Mathf.Clamp(anchor.x, safe.xMin + halfWidth + 8f, safe.xMax - halfWidth - 8f),
            Mathf.Clamp(anchor.y, safe.yMin + halfHeight + 8f, safe.yMax - halfHeight - 8f));

        if (progress >= 0f)
        {
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            float pulse = !VibeSettings.ReduceEffects ? Mathf.Clamp01((pulseUntil - Time.unscaledTime) / .19f) : 0f;
            progressFill.color = Color.Lerp(Gold, Green, pulse);
        }
        if (meter)
        {
            pointer.anchoredPosition = new Vector2(marker * timingTrack.rect.width, 0f);
            Color muted = Color.Lerp(Green, Track, .56f);
            zone.color = attempted && !precise ? muted : Green;
            pointerImage.color = precise ? Green : Gold;
        }
    }

    void ApplyLayout()
    {
        bool withText = !string.IsNullOrEmpty(label);
        bool withProgress = progress >= 0f;
        float width = withText ? Mathf.Clamp(prompt.preferredWidth + 28f, 148f, 345f) : 148f;
        float height = 14f + (withText ? 22f : 0f) + (withProgress ? 8f : 0f) + (meter ? 16f : 0f);
        if (Mathf.Approximately(width, cardWidth) && Mathf.Approximately(height, cardHeight)) return;
        cardWidth = width;
        cardHeight = height;
        card.sizeDelta = new Vector2(width, height);
        float y = height * .5f - 7f;
        if (withText)
        {
            Place(prompt.rectTransform, width - 20f, 22f, y - 11f);
            y -= 22f;
        }
        progressTrack.gameObject.SetActive(withProgress);
        if (withProgress)
        {
            Place(progressTrack, width - 24f, 3f, y - 3f);
            y -= 8f;
        }
        timingTrack.gameObject.SetActive(meter);
        if (meter)
        {
            float span = width - 24f;
            Place(timingTrack, span, 7f, y - 8f);
            Place(zone.rectTransform, span * 2f * Chop03Balance.GreenHalfWidth, 7f, 0f);
        }
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
