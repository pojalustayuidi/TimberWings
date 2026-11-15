using UnityEngine;
using UnityEngine.EventSystems;

// Interactive illustrations on Leo's journal. Never mutates quest or save state.
// v1.9 final: gentle parallax reaction to the cursor, hover hint after a short delay,
// and a bounds guard that keeps the (animated) illustration inside the visible page.
public sealed class TWJournalMenuEasterEgg : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public TWJournalMenuController menu;
    [TextArea] public string message = "Любопытная находка...";
    public float hoverAngle = 2.5f;
    public float hoverScale = 1.035f;

    [Header("Raised paper feel")]
    [Range(0f, 16f)] public float hoverLiftPixels = 5f;
    [Range(0f, 3f)] public float idleFloatPixels = 1.1f;
    [Range(0.01f, 1f)] public float idleSpeed = .19f;

    [Header("Cursor reaction")]
    [Range(0f, 6f)] public float parallaxPixels = 2.5f;
    [Range(-1.5f, 1.5f)] public float parallaxDepth = 1f;
    [Range(0f, 2f)] public float hoverHintDelay = .55f;

    [Header("Visible page guard")]
    [Tooltip("Invisible rect of the safe page area (RightPage_SafeArea). Empty = no guard.")]
    public RectTransform pageBounds;
    [Range(0f, 20f)] public float boundsPadding = 2f;

    bool hovered, hintShownThisHover;
    float hoverStart;
    Quaternion baseRotation;
    Vector3 baseScale;
    RectTransform rect;
    Vector2 basePosition;
    float tapUntil;
    Rect allowed;
    bool haveAllowed;
    static readonly Vector3[] corners = new Vector3[4];

    void Awake()
    {
        baseRotation = transform.localRotation;
        baseScale = transform.localScale;
        rect = transform as RectTransform;
        if (rect != null) basePosition = rect.anchoredPosition;
    }

    void OnEnable() { haveAllowed = false; }

    void OnDisable()
    {
        hovered = false;
        transform.localRotation = baseRotation;
        transform.localScale = baseScale;
        if (rect != null) rect.anchoredPosition = basePosition;
    }

    public void OnPointerEnter(PointerEventData data)
    {
        hovered = true; hintShownThisHover = false; hoverStart = Time.unscaledTime;
    }
    public void OnPointerExit(PointerEventData data) { hovered = false; }

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        if (menu != null)
        {
            menu.ShowLoreHint(message);
            menu.PlayLoreSound();
            hintShownThisHover = true;
        }
        tapUntil = Time.unscaledTime + .15f;
    }

    // Bounding box of this illustration in the local space of pageBounds.
    bool TryGetLocalBounds(out Rect r)
    {
        r = default(Rect);
        if (rect == null || pageBounds == null) return false;
        rect.GetWorldCorners(corners);
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = pageBounds.InverseTransformPoint(corners[i]);
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        r = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    void CaptureAllowed()
    {
        haveAllowed = true;
        Rect page = pageBounds.rect;
        page.xMin += boundsPadding; page.yMin += boundsPadding;
        page.xMax -= boundsPadding; page.yMax -= boundsPadding;
        Rect b;
        // The guard never moves an illustration that was *placed* partly outside by hand:
        // it may only occupy the page area plus whatever its resting pose already occupies.
        if (TryGetLocalBounds(out b))
            allowed = Rect.MinMaxRect(Mathf.Min(page.xMin, b.xMin), Mathf.Min(page.yMin, b.yMin),
                                      Mathf.Max(page.xMax, b.xMax), Mathf.Max(page.yMax, b.yMax));
        else allowed = page;
    }

    void Update()
    {
        bool reduced = VibeSettings.ReduceEffects;
        bool active = hovered && !reduced;
        float k = 1f - Mathf.Exp(-13f * Time.unscaledDeltaTime);
        float scale = reduced ? 1f : Time.unscaledTime < tapUntil ? .975f : active ? hoverScale : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * scale, k);
        transform.localRotation = Quaternion.Slerp(transform.localRotation,
            baseRotation * Quaternion.Euler(0f, 0f, active ? hoverAngle : 0f), k);

        if (rect != null)
        {
            float idle = reduced ? 0f :
                Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f * idleSpeed) + basePosition.x * .018f) * idleFloatPixels;
            Vector2 parallax = Vector2.zero;
            if (!reduced && menu != null && !Application.isMobilePlatform)
                parallax = -menu.CursorNormalized * (parallaxPixels * parallaxDepth);
            Vector2 target = basePosition + parallax + Vector2.up * (idle + (active ? hoverLiftPixels : 0f));
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, k);

            if (pageBounds != null)
            {
                if (!haveAllowed) CaptureAllowed();
                Rect cur;
                if (TryGetLocalBounds(out cur))
                {
                    float dx = Mathf.Max(0f, allowed.xMin - cur.xMin) - Mathf.Max(0f, cur.xMax - allowed.xMax);
                    float dy = Mathf.Max(0f, allowed.yMin - cur.yMin) - Mathf.Max(0f, cur.yMax - allowed.yMax);
                    if (dx != 0f || dy != 0f) rect.anchoredPosition += new Vector2(dx, dy);
                }
            }
        }

        // Short story hint after a calm hover (silent; the click adds the sound).
        if (hovered && !hintShownThisHover && menu != null && Time.unscaledTime - hoverStart >= hoverHintDelay)
        {
            hintShownThisHover = true;
            menu.ShowLoreHint(message);
        }
    }
}
